using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Imagino.Api.DTOs.Image;
using Imagino.Api.Models.Image;
using Imagino.Api.Repositories.Image;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

        public PublicImageModelsController(
            IImageModelRepository modelRepository,
            IImageModelVersionRepository versionRepository,
            IImageModelPresetRepository presetRepository)
        {
            _modelRepository = modelRepository;
            _versionRepository = versionRepository;
            _presetRepository = presetRepository;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<PublicImageModelSummaryDto>>> List(
            [FromQuery] ImageModelVisibility visibility = ImageModelVisibility.Public,
            [FromQuery(Name = "include")] string? include = null)
        {
            var includeSet = ParseInclude(include);
            var models = await _modelRepository.GetAsync(visibility: visibility);

            var response = new List<PublicImageModelSummaryDto>();
            foreach (var model in models.Where(m => ListAllowedStatuses.Contains(m.Status)))
            {
                if (string.IsNullOrWhiteSpace(model.Id))
                {
                    continue;
                }

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

                if (includeSet.Contains("presets"))
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

                response.Add(summary);
            }

            return Ok(response.OrderBy(r => r.DisplayName));
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

        private static HashSet<string> ParseInclude(string? include) =>
            (include ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => part.ToLowerInvariant())
                .ToHashSet();

        private static bool IsPresetVisibleFor(ImageModelVisibility requested, ImageModelVisibility presetVisibility) => requested switch
        {
            ImageModelVisibility.Public => presetVisibility == ImageModelVisibility.Public,
            ImageModelVisibility.Premium => presetVisibility is ImageModelVisibility.Public or ImageModelVisibility.Premium,
            ImageModelVisibility.Internal => true,
            _ => false
        };

        private static ImageModelCapabilities CloneCapabilities(ImageModelCapabilities capabilities) => new()
        {
            Image = capabilities?.Image ?? false,
            Inpaint = capabilities?.Inpaint ?? false,
            Upscale = capabilities?.Upscale ?? false
        };
    }
}
