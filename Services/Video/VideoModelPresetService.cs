using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Imagino.Api.Errors;
using Imagino.Api.Models.Video;
using Imagino.Api.Repositories.Video;

namespace Imagino.Api.Services.Video
{
    public class VideoModelPresetService : IVideoModelPresetService
    {
        private readonly IVideoModelPresetRepository _presetRepository;
        private readonly IVideoModelRepository _modelRepository;
        private readonly IVideoModelVersionRepository _versionRepository;

        public VideoModelPresetService(
            IVideoModelPresetRepository presetRepository,
            IVideoModelRepository modelRepository,
            IVideoModelVersionRepository versionRepository)
        {
            _presetRepository = presetRepository;
            _modelRepository = modelRepository;
            _versionRepository = versionRepository;
        }

        public async Task<List<VideoModelPreset>> ListByModelAsync(string modelId, VideoModelPresetStatus? status)
        {
            await EnsureModelExists(modelId);
            return await _presetRepository.GetByModelIdAsync(modelId, status);
        }

        public async Task<VideoModelPreset?> GetByIdAsync(string id)
        {
            return await _presetRepository.GetByIdAsync(id);
        }

        public async Task<VideoModelPreset> CreateAsync(VideoModelPreset preset)
        {
            await EnsureModelAndVersion(preset.ModelId, preset.ModelVersionId);
            await EnsureSlugUnique(preset.Slug);

            preset.CreatedAt = DateTime.UtcNow;
            preset.UpdatedAt = DateTime.UtcNow;

            await _presetRepository.InsertAsync(preset);
            return preset;
        }

        public async Task<VideoModelPreset?> UpdateAsync(string id, VideoModelPreset preset)
        {
            var existing = await _presetRepository.GetByIdAsync(id);
            if (existing == null)
            {
                return null;
            }

            await EnsureModelAndVersion(preset.ModelId, preset.ModelVersionId);
            if (!string.Equals(existing.Slug, preset.Slug, StringComparison.OrdinalIgnoreCase))
            {
                await EnsureSlugUnique(preset.Slug);
            }

            existing.ModelId = preset.ModelId;
            existing.ModelVersionId = preset.ModelVersionId;
            existing.Slug = preset.Slug;
            existing.Name = preset.Name;
            existing.Description = preset.Description;
            existing.Status = preset.Status;
            existing.Params = preset.Params;
            existing.Locks = preset.Locks;
            existing.UpdatedAt = DateTime.UtcNow;

            await _presetRepository.UpdateAsync(existing);
            return existing;
        }

        public async Task DeleteAsync(string id)
        {
            await _presetRepository.DeleteAsync(id);
        }

        private async Task EnsureModelExists(string modelId)
        {
            var model = await _modelRepository.GetByIdAsync(modelId);
            if (model == null)
            {
                throw new ValidationAppException($"Model '{modelId}' not found");
            }
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

        private async Task EnsureSlugUnique(string slug)
        {
            var existing = await _presetRepository.GetBySlugAsync(slug);
            if (existing != null)
            {
                throw new ValidationAppException($"Preset slug '{slug}' is already in use");
            }
        }
    }
}
