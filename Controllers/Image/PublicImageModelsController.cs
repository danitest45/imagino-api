using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Imagino.Api.DTOs;
using Imagino.Api.DTOs.Image;
using Imagino.Api.Models.Image;
using Imagino.Api.Repositories.Image;
using Imagino.Api.Services.Image;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

namespace Imagino.Api.Controllers.Image
{
    [ApiController]
    [Route("api/image/models")]
    public class PublicImageModelsController : ControllerBase
    {
        private static readonly HashSet<ImageModelStatus> ListAllowedStatuses = new()
        {
            ImageModelStatus.Active,
            ImageModelStatus.Deprecated
        };

        private static readonly HashSet<ImageModelVersionStatus> VersionAllowedStatuses = new()
        {
            ImageModelVersionStatus.Active,
            ImageModelVersionStatus.Canary,
            ImageModelVersionStatus.Deprecated
        };

        private readonly IImageModelRepository _modelRepository;
        private readonly IImageModelVersionRepository _versionRepository;
        private readonly IImageModelPresetRepository _presetRepository;
        private readonly IMemoryCache _cache;
        private readonly IPublicImageModelCacheService _cacheService;

        public PublicImageModelsController(
            IImageModelRepository modelRepository,
            IImageModelVersionRepository versionRepository,
            IImageModelPresetRepository presetRepository,
            IMemoryCache cache,
            IPublicImageModelCacheService cacheService)
        {
            _modelRepository = modelRepository;
            _versionRepository = versionRepository;
            _presetRepository = presetRepository;
            _cache = cache;
            _cacheService = cacheService;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<PagedResult<PublicImageModelSummaryDto>>> List(
            [FromQuery] ImageModelVisibility visibility = ImageModelVisibility.Public,
            [FromQuery(Name = "include")] string? include = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var safePage = Math.Max(1, page);
            var safePageSize = Math.Clamp(pageSize, 1, 100);
            var includeSet = ParseInclude(include);
            var cacheKey = BuildCacheKey(visibility, includeSet, safePage, safePageSize, _cacheService.GetVersion());
            if (_cache.TryGetValue<PagedResult<PublicImageModelSummaryDto>>(cacheKey, out var cached))
            {
                return Ok(cached);
            }

            var models = await _modelRepository.GetAsync(visibility: visibility);

            var filtered = models
                .Where(m => ListAllowedStatuses.Contains(m.Status) && !string.IsNullOrWhiteSpace(m.Id))
                .OrderBy(m => m.DisplayName)
                .ToList();

            var response = new List<PublicImageModelSummaryDto>();

            foreach (var model in filtered.Skip((safePage - 1) * safePageSize).Take(safePageSize))
            {
                var summary = await BuildSummaryAsync(model, includeSet, visibility);
                response.Add(summary);
            }

            var paged = new PagedResult<PublicImageModelSummaryDto>
            {
                Items = response,
                Total = filtered.Count
            };

            _cache.Set(cacheKey, paged, TimeSpan.FromMinutes(10));
            return Ok(paged);
        }

        [HttpGet("{slug}")]
        [AllowAnonymous]
        public async Task<ActionResult<PublicImageModelDetailsDto>> GetBySlug(
            string slug,
            [FromQuery(Name = "include")] string? include = null)
        {
            var includeSet = ParseInclude(include);
            var model = await _modelRepository.GetBySlugAsync(slug);
            if (model == null || model.Status == ImageModelStatus.Archived)
            {
                return NotFound();
            }

            var details = new PublicImageModelDetailsDto
            {
                Slug = model.Slug,
                DisplayName = model.DisplayName,
                Capabilities = CloneCapabilities(model.Capabilities),
                Visibility = model.Visibility,
                Status = model.Status
            };

            if (!string.IsNullOrWhiteSpace(model.DefaultVersionId))
            {
                var defaultVersion = await _versionRepository.GetByIdAsync(model.DefaultVersionId);
                if (defaultVersion != null && VersionAllowedStatuses.Contains(defaultVersion.Status))
                {
                    details.DefaultVersionTag = defaultVersion.VersionTag;
                }
            }

            if (includeSet.Contains("versions") && !string.IsNullOrWhiteSpace(model.Id))
            {
                var versions = await _versionRepository.GetByModelIdAsync(model.Id!);
                details.Versions = versions
                    .Where(v => VersionAllowedStatuses.Contains(v.Status))
                    .OrderByDescending(v => v.CreatedAt)
                    .Select(v => new PublicImageModelVersionSummaryDto
                    {
                        VersionTag = v.VersionTag,
                        Status = v.Status
                    })
                    .ToList();
            }

            if (includeSet.Contains("presets") && !string.IsNullOrWhiteSpace(model.Id))
            {
                var presets = await _presetRepository.GetByModelIdAsync(model.Id!, ImageModelPresetStatus.Active);
                details.Presets = presets
                    .OrderBy(p => p.Ordering)
                    .Select(p => new PublicImageModelPresetSummaryDto
                    {
                        Id = p.Id!,
                        Slug = p.Slug,
                        Name = p.Name,
                        Description = p.Description,
                        Thumbnail = p.Thumbnail,
                        Ordering = p.Ordering,
                        Visibility = p.Visibility
                    })
                    .ToList();
            }

            return Ok(details);
        }

        [HttpGet("{slug}/versions/{versionTag}")]
        [AllowAnonymous]
        public async Task<ActionResult<PublicImageModelVersionDetailsDto>> GetVersionDetails(string slug, string versionTag)
        {
            var model = await _modelRepository.GetBySlugAsync(slug);
            if (model == null || model.Status == ImageModelStatus.Archived)
            {
                return NotFound();
            }

            var version = await _versionRepository.GetByModelAndTagAsync(model.Id!, versionTag);
            if (version == null || version.Status == ImageModelVersionStatus.Archived)
            {
                return NotFound();
            }

            static JsonDocument? ToJson(BsonDocument? bson) => bson == null
                ? null
                : JsonDocument.Parse(bson.ToJson(new MongoDB.Bson.IO.JsonWriterSettings
                {
                    OutputMode = MongoDB.Bson.IO.JsonOutputMode.CanonicalExtendedJson
                }));

            var dto = new PublicImageModelVersionDetailsDto
            {
                ModelSlug = model.Slug,
                VersionTag = version.VersionTag,
                Status = version.Status,
                ParamSchema = ToJson(version.ParamSchema),
                Defaults = ToJson(version.Defaults),
                Limits = version.Limits == null ? null : new ImageModelVersionLimitsDto
                {
                    MaxWidth = version.Limits.MaxWidth,
                    MaxHeight = version.Limits.MaxHeight,
                    Formats = version.Limits.Formats
                },
                Pricing = version.Pricing == null ? null : new ImageModelPricingDto
                {
                    CreditsPerImage = version.Pricing.CreditsPerImage,
                    MinCredits = version.Pricing.MinCredits
                },
                ReleaseNotes = version.ReleaseNotes
            };

            return Ok(dto);
        }

        private static HashSet<string> ParseInclude(string? include) =>
            (include ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => part.ToLowerInvariant())
                .ToHashSet();

        private static string BuildCacheKey(ImageModelVisibility visibility, HashSet<string> include, int page, int pageSize, int version)
        {
            var includeKey = string.Join('-', include.OrderBy(v => v));
            return $"public-models-{visibility}-{includeKey}-p{page}-ps{pageSize}-v{version}";
        }

        private static bool IsPresetVisibleFor(ImageModelVisibility requested, ImageModelVisibility presetVisibility) => requested switch
        {
            ImageModelVisibility.Public => presetVisibility == ImageModelVisibility.Public,
            ImageModelVisibility.Premium => presetVisibility is ImageModelVisibility.Public or ImageModelVisibility.Premium,
            ImageModelVisibility.Internal => true,
            _ => false
        };

        private async Task<PublicImageModelSummaryDto> BuildSummaryAsync(ImageModel model, HashSet<string> includeSet, ImageModelVisibility visibility)
        {
            var summary = new PublicImageModelSummaryDto
            {
                Slug = model.Slug,
                DisplayName = model.DisplayName,
                Capabilities = CloneCapabilities(model.Capabilities),
                Visibility = model.Visibility,
                Status = model.Status
            };

            if (includeSet.Contains("defaultversion") && !string.IsNullOrWhiteSpace(model.DefaultVersionId))
            {
                var defaultVersion = await _versionRepository.GetByIdAsync(model.DefaultVersionId);
                if (defaultVersion != null && VersionAllowedStatuses.Contains(defaultVersion.Status))
                {
                    summary.DefaultVersionTag = defaultVersion.VersionTag;
                }
            }

            if (includeSet.Contains("presets") && !string.IsNullOrWhiteSpace(model.Id))
            {
                var presets = await _presetRepository.GetByModelIdAsync(model.Id!, ImageModelPresetStatus.Active);
                summary.Presets = presets
                    .Where(p => IsPresetVisibleFor(visibility, p.Visibility))
                    .OrderBy(p => p.Ordering)
                    .Select(p => new PublicImageModelPresetSummaryDto
                    {
                        Id = p.Id!,
                        Slug = p.Slug,
                        Name = p.Name,
                        Description = p.Description,
                        Thumbnail = p.Thumbnail,
                        Ordering = p.Ordering,
                        Visibility = p.Visibility
                    })
                    .ToList();
            }

            return summary;
        }

        private static ImageModelCapabilities CloneCapabilities(ImageModelCapabilities capabilities) => new()
        {
            Image = capabilities?.Image ?? false,
            Inpaint = capabilities?.Inpaint ?? false,
            Upscale = capabilities?.Upscale ?? false
        };
    }
}
