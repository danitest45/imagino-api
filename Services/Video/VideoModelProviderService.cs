using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Imagino.Api.Models.Video;
using Imagino.Api.Repositories.Video;

namespace Imagino.Api.Services.Video
{
    public class VideoModelProviderService : IVideoModelProviderService
    {
        private readonly IVideoModelProviderRepository _providerRepository;

        public VideoModelProviderService(IVideoModelProviderRepository providerRepository)
        {
            _providerRepository = providerRepository;
        }

        public async Task<List<VideoModelProvider>> ListAsync()
        {
            return await _providerRepository.GetAsync();
        }

        public async Task<VideoModelProvider?> GetByIdAsync(string id)
        {
            return await _providerRepository.GetByIdAsync(id);
        }

        public async Task<VideoModelProvider> CreateAsync(VideoModelProvider provider)
        {
            provider.CreatedAt = DateTime.UtcNow;
            provider.UpdatedAt = DateTime.UtcNow;

            await _providerRepository.InsertAsync(provider);
            return provider;
        }

        public async Task<VideoModelProvider?> UpdateAsync(string id, VideoModelProvider provider)
        {
            var existing = await _providerRepository.GetByIdAsync(id);
            if (existing == null)
            {
                return null;
            }

            existing.Name = provider.Name;
            existing.ProviderType = provider.ProviderType;
            existing.Config = provider.Config;
            existing.UpdatedAt = DateTime.UtcNow;

            await _providerRepository.UpdateAsync(existing);
            return existing;
        }
    }
}
