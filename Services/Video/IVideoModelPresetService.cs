using Imagino.Api.Models.Video;

namespace Imagino.Api.Services.Video
{
    public interface IVideoModelPresetService
    {
        Task<List<VideoModelPreset>> ListByModelAsync(string modelId, VideoModelPresetStatus? status);
        Task<VideoModelPreset?> GetByIdAsync(string id);
        Task<VideoModelPreset> CreateAsync(VideoModelPreset preset);
        Task<VideoModelPreset?> UpdateAsync(string id, VideoModelPreset preset);
        Task DeleteAsync(string id);
    }
}
