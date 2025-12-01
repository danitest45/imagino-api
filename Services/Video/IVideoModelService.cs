using Imagino.Api.Models.Video;

namespace Imagino.Api.Services.Video
{
    public interface IVideoModelService
    {
        Task<List<VideoModel>> ListAsync(VideoModelStatus? status, VideoModelVisibility? visibility);
        Task<VideoModel?> GetBySlugAsync(string slug);
        Task<VideoModel?> GetByIdAsync(string id);
        Task<VideoModel> CreateAsync(VideoModel model);
        Task<VideoModel?> UpdateAsync(string id, VideoModel model);
        Task DeleteAsync(string id);
        Task SetDefaultVersionAsync(string modelId, string versionId);
    }
}
