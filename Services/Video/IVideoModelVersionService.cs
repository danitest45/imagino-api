using Imagino.Api.Models.Video;

namespace Imagino.Api.Services.Video
{
    public interface IVideoModelVersionService
    {
        Task<List<VideoModelVersion>> ListByModelAsync(string modelId, VideoModelVersionStatus? status);
        Task<VideoModelVersion?> GetByModelAndTagAsync(string modelId, string versionTag);
        Task<VideoModelVersion?> GetByIdAsync(string id);
        Task<VideoModelVersion> CreateAsync(VideoModelVersion version);
        Task<VideoModelVersion?> UpdateAsync(string id, VideoModelVersion version);
        Task DeleteAsync(string id);
    }
}
