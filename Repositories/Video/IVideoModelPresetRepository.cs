using Imagino.Api.Models.Video;

namespace Imagino.Api.Repositories.Video
{
    public interface IVideoModelPresetRepository
    {
        Task<VideoModelPreset?> GetByIdAsync(string id);
        Task<VideoModelPreset?> GetBySlugAsync(string slug);
        Task<List<VideoModelPreset>> GetByModelIdAsync(string modelId, VideoModelPresetStatus? status = null);
        Task InsertAsync(VideoModelPreset preset);
        Task UpdateAsync(VideoModelPreset preset);
        Task DeleteAsync(string id);
    }
}
