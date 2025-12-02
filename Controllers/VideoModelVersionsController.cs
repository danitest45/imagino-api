using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Imagino.Api.DTOs.Video;
using Imagino.Api.Models.Video;
using Imagino.Api.Services.Video;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

namespace Imagino.Api.Controllers
{
    [ApiController]
    [Route("api/video/models/{modelId}/versions")]
    public class VideoModelVersionsController : ControllerBase
    {
        private readonly IVideoModelVersionService _versionService;
        private readonly IVideoModelService _modelService;

        public VideoModelVersionsController(IVideoModelVersionService versionService, IVideoModelService modelService)
        {
            _versionService = versionService;
            _modelService = modelService;
        }

        [HttpGet]
        public async Task<ActionResult<List<VideoModelVersionDto>>> List(string modelId, [FromQuery] VideoModelVersionStatus? status)
        {
            var versions = await _versionService.ListByModelAsync(modelId, status);
            var dtos = versions.Select(ToDto).ToList();
            return Ok(dtos);
        }

        [HttpGet("{versionTag}")]
        public async Task<ActionResult<VideoModelVersionDto>> Get(string modelId, string versionTag)
        {
            var version = await _versionService.GetByModelAndTagAsync(modelId, versionTag);
            if (version == null)
            {
                return NotFound();
            }

            return Ok(ToDto(version));
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult<VideoModelVersionDto>> Create(string modelId, [FromBody] CreateVideoModelVersionDto dto)
        {
            var version = new VideoModelVersion
            {
                ModelId = modelId,
                VersionTag = dto.VersionTag,
                EndpointUrl = dto.EndpointUrl,
                ParamSchema = ConvertJsonToBson(dto.ParamSchema),
                Defaults = ConvertJsonToBson(dto.Defaults),
                Limits = dto.Limits == null ? null : new VideoModelVersionLimits
                {
                    MaxDurationSeconds = dto.Limits.MaxDurationSeconds,
                    Resolutions = dto.Limits.Resolutions
                },
                Pricing = dto.Pricing == null ? null : new VideoModelPricing
                {
                    CreditsPerSecond = dto.Pricing.CreditsPerSecond,
                    CreditsPerVideo = dto.Pricing.CreditsPerVideo,
                    MinCredits = dto.Pricing.MinCredits
                },
                Status = dto.Status,
                Rollout = dto.Rollout == null ? null : new VideoModelVersionRollout
                {
                    CanaryPercent = dto.Rollout.CanaryPercent
                },
                ReleaseNotes = dto.ReleaseNotes
            };

            var created = await _versionService.CreateAsync(version);
            return CreatedAtAction(nameof(Get), new { modelId, versionTag = created.VersionTag }, ToDto(created));
        }

        [HttpPut("{versionId}")]
        [Authorize]
        public async Task<ActionResult<VideoModelVersionDto>> Update(string modelId, string versionId, [FromBody] UpdateVideoModelVersionDto dto)
        {
            var version = new VideoModelVersion
            {
                ModelId = modelId,
                VersionTag = dto.VersionTag,
                EndpointUrl = dto.EndpointUrl,
                ParamSchema = ConvertJsonToBson(dto.ParamSchema),
                Defaults = ConvertJsonToBson(dto.Defaults),
                Limits = dto.Limits == null ? null : new VideoModelVersionLimits
                {
                    MaxDurationSeconds = dto.Limits.MaxDurationSeconds,
                    Resolutions = dto.Limits.Resolutions
                },
                Pricing = dto.Pricing == null ? null : new VideoModelPricing
                {
                    CreditsPerSecond = dto.Pricing.CreditsPerSecond,
                    CreditsPerVideo = dto.Pricing.CreditsPerVideo,
                    MinCredits = dto.Pricing.MinCredits
                },
                Status = dto.Status,
                Rollout = dto.Rollout == null ? null : new VideoModelVersionRollout
                {
                    CanaryPercent = dto.Rollout.CanaryPercent
                },
                ReleaseNotes = dto.ReleaseNotes
            };

            var updated = await _versionService.UpdateAsync(versionId, version);
            if (updated == null)
            {
                return NotFound();
            }

            return Ok(ToDto(updated));
        }

        [HttpDelete("{versionId}")]
        [Authorize]
        public async Task<IActionResult> Delete(string modelId, string versionId)
        {
            await _versionService.DeleteAsync(versionId);
            return NoContent();
        }

        [HttpPost("{versionId}/set-default")]
        [Authorize]
        public async Task<IActionResult> SetDefault(string modelId, string versionId)
        {
            var model = await _modelService.GetByIdAsync(modelId);
            var version = await _versionService.GetByIdAsync(versionId);
            if (model == null || version == null || version.ModelId != modelId)
            {
                return NotFound();
            }

            await _modelService.SetDefaultVersionAsync(modelId, versionId);
            return NoContent();
        }

        private static VideoModelVersionDto ToDto(VideoModelVersion version) => new()
        {
            Id = version.Id,
            ModelId = version.ModelId,
            VersionTag = version.VersionTag,
            EndpointUrl = version.EndpointUrl,
            ParamSchema = version.ParamSchema == null ? null : JsonDocument.Parse(version.ParamSchema.ToJson()),
            Defaults = version.Defaults == null ? null : JsonDocument.Parse(version.Defaults.ToJson()),
            Limits = version.Limits == null ? null : new VideoModelVersionLimitsDto
            {
                MaxDurationSeconds = version.Limits.MaxDurationSeconds,
                Resolutions = version.Limits.Resolutions
            },
            Pricing = version.Pricing == null ? null : new VideoModelPricingDto
            {
                CreditsPerSecond = version.Pricing.CreditsPerSecond,
                CreditsPerVideo = version.Pricing.CreditsPerVideo,
                MinCredits = version.Pricing.MinCredits
            },
            Status = version.Status,
            Rollout = version.Rollout == null ? null : new VideoModelVersionRolloutDto
            {
                CanaryPercent = version.Rollout.CanaryPercent
            },
            ReleaseNotes = version.ReleaseNotes,
            CreatedAt = version.CreatedAt,
            UpdatedAt = version.UpdatedAt
        };

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
    }
}
