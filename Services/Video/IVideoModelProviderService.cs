using Imagino.Api.Models.Video;

namespace Imagino.Api.Services.Video
{
    public interface IVideoModelProviderService
    {
        Task<List<VideoModelProvider>> ListAsync();
        Task<VideoModelProvider?> GetByIdAsync(string id);
        Task<VideoModelProvider> CreateAsync(VideoModelProvider provider);
        Task<VideoModelProvider?> UpdateAsync(string id, VideoModelProvider provider);
    }
}
