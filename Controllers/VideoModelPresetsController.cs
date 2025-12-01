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
    [Route("api/video/models/{modelId}/presets")]
    public class VideoModelPresetsController : ControllerBase
    {
        private readonly IVideoModelPresetService _presetService;

        public VideoModelPresetsController(IVideoModelPresetService presetService)
        {
            _presetService = presetService;
        }

        [HttpGet]
        public async Task<ActionResult<List<VideoModelPresetDto>>> List(string modelId, [FromQuery] VideoModelPresetStatus? status)
        {
            var presets = await _presetService.ListByModelAsync(modelId, status);
            var dtos = presets.Select(ToDto).ToList();
            return Ok(dtos);
        }

        [HttpGet("{presetId}")]
        public async Task<ActionResult<VideoModelPresetDto>> Get(string modelId, string presetId)
        {
            var preset = await _presetService.GetByIdAsync(presetId);
            if (preset == null || preset.ModelId != modelId)
            {
                return NotFound();
            }

            return Ok(ToDto(preset));
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult<VideoModelPresetDto>> Create(string modelId, [FromBody] VideoModelPresetDto dto)
        {
            var preset = new VideoModelPreset
            {
                ModelId = modelId,
                ModelVersionId = dto.ModelVersionId,
                Slug = dto.Slug,
                Name = dto.Name,
                Description = dto.Description,
                Status = dto.Status,
                Params = ConvertJsonToBson(dto.Params),
                Locks = dto.Locks
            };

            var created = await _presetService.CreateAsync(preset);
            return CreatedAtAction(nameof(Get), new { modelId, presetId = created.Id }, ToDto(created));
        }

        [HttpPut("{presetId}")]
        [Authorize]
        public async Task<ActionResult<VideoModelPresetDto>> Update(string modelId, string presetId, [FromBody] VideoModelPresetDto dto)
        {
            var preset = new VideoModelPreset
            {
                ModelId = modelId,
                ModelVersionId = dto.ModelVersionId,
                Slug = dto.Slug,
                Name = dto.Name,
                Description = dto.Description,
                Status = dto.Status,
                Params = ConvertJsonToBson(dto.Params),
                Locks = dto.Locks
            };

            var updated = await _presetService.UpdateAsync(presetId, preset);
            if (updated == null || updated.ModelId != modelId)
            {
                return NotFound();
            }

            return Ok(ToDto(updated));
        }

        [HttpDelete("{presetId}")]
        [Authorize]
        public async Task<IActionResult> Delete(string modelId, string presetId)
        {
            await _presetService.DeleteAsync(presetId);
            return NoContent();
        }

        private static VideoModelPresetDto ToDto(VideoModelPreset preset) => new()
        {
            Id = preset.Id,
            ModelId = preset.ModelId,
            ModelVersionId = preset.ModelVersionId,
            Slug = preset.Slug,
            Name = preset.Name,
            Description = preset.Description,
            Status = preset.Status,
            Params = preset.Params == null ? null : JsonDocument.Parse(preset.Params.ToJson()),
            Locks = preset.Locks,
            CreatedAt = preset.CreatedAt,
            UpdatedAt = preset.UpdatedAt
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
