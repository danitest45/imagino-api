using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Imagino.Api.Errors;
using Imagino.Api.Models.Video;
using Imagino.Api.Repositories.Video;

namespace Imagino.Api.Services.Video
{
    public class VideoModelService : IVideoModelService
    {
        private readonly IVideoModelRepository _modelRepository;
        private readonly IVideoModelProviderRepository _providerRepository;

        public VideoModelService(IVideoModelRepository modelRepository, IVideoModelProviderRepository providerRepository)
        {
            _modelRepository = modelRepository;
            _providerRepository = providerRepository;
        }

        public async Task<List<VideoModel>> ListAsync(VideoModelStatus? status, VideoModelVisibility? visibility)
        {
            return await _modelRepository.GetAsync(status, visibility);
        }

        public async Task<VideoModel?> GetBySlugAsync(string slug)
        {
            return await _modelRepository.GetBySlugAsync(slug);
        }

        public async Task<VideoModel?> GetByIdAsync(string id)
        {
            return await _modelRepository.GetByIdAsync(id);
        }

        public async Task<VideoModel> CreateAsync(VideoModel model)
        {
            await EnsureProviderExists(model.ProviderId);
            await EnsureSlugIsUnique(model.Slug);

            model.CreatedAt = DateTime.UtcNow;
            model.UpdatedAt = DateTime.UtcNow;

            await _modelRepository.InsertAsync(model);
            return model;
        }

        public async Task<VideoModel?> UpdateAsync(string id, VideoModel model)
        {
            var existing = await _modelRepository.GetByIdAsync(id);
            if (existing == null)
            {
                return null;
            }

            await EnsureProviderExists(model.ProviderId);
            if (!string.Equals(existing.Slug, model.Slug, StringComparison.OrdinalIgnoreCase))
            {
                await EnsureSlugIsUnique(model.Slug);
            }

            existing.Slug = model.Slug;
            existing.DisplayName = model.DisplayName;
            existing.ProviderId = model.ProviderId;
            existing.Capabilities = model.Capabilities;
            existing.Visibility = model.Visibility;
            existing.Status = model.Status;
            existing.DefaultVersionId = model.DefaultVersionId;
            existing.Pricing = model.Pricing;
            existing.Tags = model.Tags;
            existing.UpdatedAt = DateTime.UtcNow;

            await _modelRepository.UpdateAsync(existing);
            return existing;
        }

        public async Task DeleteAsync(string id)
        {
            await _modelRepository.DeleteAsync(id);
        }

        public async Task SetDefaultVersionAsync(string modelId, string versionId)
        {
            await _modelRepository.SetDefaultVersionAsync(modelId, versionId);
        }

        private async Task EnsureProviderExists(string providerId)
        {
            var provider = await _providerRepository.GetByIdAsync(providerId);
            if (provider == null)
            {
                throw new ValidationAppException($"Provider '{providerId}' not found");
            }
        }

        private async Task EnsureSlugIsUnique(string slug)
        {
            var existing = await _modelRepository.GetBySlugAsync(slug);
            if (existing != null)
            {
                throw new ValidationAppException($"Slug '{slug}' is already in use");
            }
        }
    }
}
