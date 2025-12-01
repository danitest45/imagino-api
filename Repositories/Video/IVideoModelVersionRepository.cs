using Imagino.Api.Models.Video;

namespace Imagino.Api.Repositories.Video
{
    public interface IVideoModelVersionRepository
    {
        Task<VideoModelVersion?> GetByIdAsync(string id);
        Task<VideoModelVersion?> GetByModelAndTagAsync(string modelId, string versionTag);
        Task<List<VideoModelVersion>> GetByModelIdAsync(string modelId, VideoModelVersionStatus? status = null);
        Task InsertAsync(VideoModelVersion version);
        Task UpdateAsync(VideoModelVersion version);
        Task DeleteAsync(string id);
    }
}
