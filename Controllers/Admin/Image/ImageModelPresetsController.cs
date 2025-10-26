using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Imagino.Api.DTOs.Image;
using Imagino.Api.Errors;
using Imagino.Api.Models.Image;
using Imagino.Api.Repositories.Image;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

namespace Imagino.Api.Controllers.Admin.Image
{
    [ApiController]
    [Route("api/admin/image/presets")]
    [Authorize]
    public class ImageModelPresetsController : ControllerBase
    {
        private readonly IImageModelPresetRepository _presetRepository;
        private readonly IImageModelRepository _modelRepository;
        private readonly IImageModelVersionRepository _versionRepository;

        public ImageModelPresetsController(
            IImageModelPresetRepository presetRepository,
            IImageModelRepository modelRepository,
            IImageModelVersionRepository versionRepository)
        {
            _presetRepository = presetRepository;
            _modelRepository = modelRepository;
            _versionRepository = versionRepository;
        }

        [HttpGet]
        public async Task<IActionResult> List([FromQuery] string? modelId, [FromQuery] ImageModelPresetStatus? status)
        {
            if (string.IsNullOrWhiteSpace(modelId))
            {
                return BadRequest("modelId query parameter is required");
            }

            var presets = await _presetRepository.GetByModelIdAsync(modelId, status);
            var result = new List<ImageModelPresetDto>();
            foreach (var preset in presets)
            {
                result.Add(ToDto(preset));
            }

            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var preset = await _presetRepository.GetByIdAsync(id);
            if (preset == null)
            {
                return NotFound();
            }

            return Ok(ToDto(preset));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateImageModelPresetDto dto)
        {
            await EnsureModelAndVersion(dto.ModelId, dto.ModelVersionId);

            var existing = await _presetRepository.GetBySlugAsync(dto.ModelId, dto.Slug);
            if (existing != null)
            {
                throw new ConflictAppException($"Preset with slug '{dto.Slug}' already exists for this model");
            }

            var preset = new ImageModelPreset
            {
                ModelId = dto.ModelId,
                ModelVersionId = dto.ModelVersionId,
                Slug = dto.Slug,
                Name = dto.Name,
                Description = dto.Description,
                Thumbnail = dto.Thumbnail,
                Params = ConvertJsonToBson(dto.Params),
                Locks = dto.Locks,
                Visibility = dto.Visibility,
                Status = dto.Status,
                Ordering = dto.Ordering,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _presetRepository.InsertAsync(preset);
            return CreatedAtAction(nameof(GetById), new { id = preset.Id }, ToDto(preset));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] UpdateImageModelPresetDto dto)
        {
            var preset = await _presetRepository.GetByIdAsync(id);
            if (preset == null)
            {
                return NotFound();
            }

            await EnsureModelAndVersion(dto.ModelId, dto.ModelVersionId);

            preset.ModelId = dto.ModelId;
            preset.ModelVersionId = dto.ModelVersionId;
            preset.Slug = dto.Slug;
            preset.Name = dto.Name;
            preset.Description = dto.Description;
            preset.Thumbnail = dto.Thumbnail;
            preset.Params = ConvertJsonToBson(dto.Params);
            preset.Locks = dto.Locks;
            preset.Visibility = dto.Visibility;
            preset.Status = dto.Status;
            preset.Ordering = dto.Ordering;
            preset.UpdatedAt = DateTime.UtcNow;

            await _presetRepository.UpdateAsync(preset);
            return Ok(ToDto(preset));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            await _presetRepository.DeleteAsync(id);
            return NoContent();
        }

        private async Task EnsureModelAndVersion(string modelId, string versionId)
        {
            var model = await _modelRepository.GetByIdAsync(modelId);
            if (model == null)
            {
                throw new ValidationAppException($"Model '{modelId}' not found");
            }

            var version = await _versionRepository.GetByIdAsync(versionId);
            if (version == null || version.ModelId != modelId)
            {
                throw new ValidationAppException($"Version '{versionId}' not found for model '{modelId}'");
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

        private static ImageModelPresetDto ToDto(ImageModelPreset preset) => new()
        {
            Id = preset.Id,
            ModelId = preset.ModelId,
            ModelVersionId = preset.ModelVersionId,
            Slug = preset.Slug,
            Name = preset.Name,
            Description = preset.Description,
            Thumbnail = preset.Thumbnail,
            Params = preset.Params == null ? null : JsonDocument.Parse(preset.Params.ToJson()),
            Locks = preset.Locks,
            Visibility = preset.Visibility,
            Status = preset.Status,
            Ordering = preset.Ordering,
            CreatedAt = preset.CreatedAt,
            UpdatedAt = preset.UpdatedAt
        };
    }
}
