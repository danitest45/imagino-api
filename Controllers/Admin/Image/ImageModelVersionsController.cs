using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Imagino.Api.DTOs.Image;
using Imagino.Api.Errors;
using Imagino.Api.Models.Image;
using Imagino.Api.Repositories.Image;
using Imagino.Api.Services.Image;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

namespace Imagino.Api.Controllers.Admin.Image
{
    [ApiController]
    [Route("api/admin/image/versions")]
        [Authorize]
        public class ImageModelVersionsController : ControllerBase
    {
        private readonly IImageModelVersionRepository _versionRepository;
        private readonly IImageModelRepository _modelRepository;
        private readonly IPublicImageModelCacheService _publicModelCacheService;

        public ImageModelVersionsController(IImageModelVersionRepository versionRepository, IImageModelRepository modelRepository, IPublicImageModelCacheService publicImageModelCacheService)
        {
            _versionRepository = versionRepository;
            _modelRepository = modelRepository;
            _publicModelCacheService = publicImageModelCacheService;
        }

        [HttpGet]
        public async Task<IActionResult> List([FromQuery] string? modelId, [FromQuery] ImageModelVersionStatus? status)
        {
            if (string.IsNullOrWhiteSpace(modelId))
            {
                return BadRequest("modelId query parameter is required");
            }

            var versions = await _versionRepository.GetByModelIdAsync(modelId, status);
            var result = new List<ImageModelVersionDto>();
            foreach (var version in versions)
            {
                result.Add(ToDto(version));
            }

            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var version = await _versionRepository.GetByIdAsync(id);
            if (version == null)
            {
                return NotFound();
            }

            return Ok(ToDto(version));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateImageModelVersionDto dto)
        {
            await EnsureModelExists(dto.ModelId);

            var version = new ImageModelVersion
            {
                ModelId = dto.ModelId,
                VersionTag = dto.VersionTag,
                EndpointUrl = dto.EndpointUrl,
                WebhookConfig = dto.WebhookConfig == null ? null : new ImageModelVersionWebhookConfig
                {
                    Url = dto.WebhookConfig.Url,
                    Events = dto.WebhookConfig.Events
                },
                ParamSchema = ConvertJsonToBson(dto.ParamSchema),
                Defaults = ConvertJsonToBson(dto.Defaults),
                Limits = dto.Limits == null ? null : new ImageModelVersionLimits
                {
                    MaxWidth = dto.Limits.MaxWidth,
                    MaxHeight = dto.Limits.MaxHeight,
                    Formats = dto.Limits.Formats
                },
                Pricing = dto.Pricing == null ? null : new ImageModelPricing
                {
                    CreditsPerImage = dto.Pricing.CreditsPerImage,
                    MinCredits = dto.Pricing.MinCredits
                },
                Status = dto.Status,
                Rollout = dto.Rollout == null ? null : new ImageModelVersionRollout
                {
                    CanaryPercent = dto.Rollout.CanaryPercent
                },
                ReleaseNotes = dto.ReleaseNotes,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _versionRepository.InsertAsync(version);
            _publicModelCacheService.BumpVersion();
            return CreatedAtAction(nameof(GetById), new { id = version.Id }, ToDto(version));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] UpdateImageModelVersionDto dto)
        {
            var version = await _versionRepository.GetByIdAsync(id);
            if (version == null)
            {
                return NotFound();
            }

            await EnsureModelExists(dto.ModelId);

            version.ModelId = dto.ModelId;
            version.VersionTag = dto.VersionTag;
            version.EndpointUrl = dto.EndpointUrl;
            version.WebhookConfig = dto.WebhookConfig == null ? null : new ImageModelVersionWebhookConfig
            {
                Url = dto.WebhookConfig.Url,
                Events = dto.WebhookConfig.Events
            };
            version.ParamSchema = ConvertJsonToBson(dto.ParamSchema);
            version.Defaults = ConvertJsonToBson(dto.Defaults);
            version.Limits = dto.Limits == null ? null : new ImageModelVersionLimits
            {
                MaxWidth = dto.Limits.MaxWidth,
                MaxHeight = dto.Limits.MaxHeight,
                Formats = dto.Limits.Formats
            };
            version.Pricing = dto.Pricing == null ? null : new ImageModelPricing
            {
                CreditsPerImage = dto.Pricing.CreditsPerImage,
                MinCredits = dto.Pricing.MinCredits
            };
            version.Status = dto.Status;
            version.Rollout = dto.Rollout == null ? null : new ImageModelVersionRollout
            {
                CanaryPercent = dto.Rollout.CanaryPercent
            };
            version.ReleaseNotes = dto.ReleaseNotes;
            version.UpdatedAt = DateTime.UtcNow;

            await _versionRepository.UpdateAsync(version);
            _publicModelCacheService.BumpVersion();
            return Ok(ToDto(version));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            await _versionRepository.DeleteAsync(id);
            _publicModelCacheService.BumpVersion();
            return NoContent();
        }

        private async Task EnsureModelExists(string modelId)
        {
            var model = await _modelRepository.GetByIdAsync(modelId);
            if (model == null)
            {
                throw new ValidationAppException($"Model '{modelId}' not found");
            }
        }

        private static BsonDocument? ConvertJsonToBson(JsonDocument? doc)
        {
            if (doc == null)
            {
                return null;
            }

            var json = doc.RootElement.GetRawText();
            return string.IsNullOrWhiteSpace(json) || json == "null"
                ? null
                : BsonDocument.Parse(json);
        }

        private static ImageModelVersionDto ToDto(ImageModelVersion version) => new()
        {
            Id = version.Id,
            ModelId = version.ModelId,
            VersionTag = version.VersionTag,
            EndpointUrl = version.EndpointUrl,
            WebhookConfig = version.WebhookConfig == null ? null : new ImageModelVersionWebhookConfigDto
            {
                Url = version.WebhookConfig.Url,
                Events = version.WebhookConfig.Events
            },
            ParamSchema = version.ParamSchema == null ? null : System.Text.Json.JsonDocument.Parse(version.ParamSchema.ToJson()),
            Defaults = version.Defaults == null ? null : System.Text.Json.JsonDocument.Parse(version.Defaults.ToJson()),
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
            Status = version.Status,
            Rollout = version.Rollout == null ? null : new ImageModelVersionRolloutDto
            {
                CanaryPercent = version.Rollout.CanaryPercent
            },
            ReleaseNotes = version.ReleaseNotes,
            CreatedAt = version.CreatedAt,
            UpdatedAt = version.UpdatedAt
        };
    }
}
