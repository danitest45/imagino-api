using Imagino.Api.Models.Video;

namespace Imagino.Api.Repositories.Video
{
    public interface IVideoModelRepository
    {
        Task<VideoModel?> GetByIdAsync(string id);
        Task<VideoModel?> GetBySlugAsync(string slug);
        Task<List<VideoModel>> GetAsync(VideoModelStatus? status = null, VideoModelVisibility? visibility = null, IEnumerable<string>? ids = null);
        Task InsertAsync(VideoModel model);
        Task UpdateAsync(VideoModel model);
        Task DeleteAsync(string id);
        Task SetDefaultVersionAsync(string modelId, string versionId);
    }
}
