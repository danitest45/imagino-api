using Imagino.Api.Models.Video;

namespace Imagino.Api.Repositories.Video
{
    public interface IVideoModelProviderRepository
    {
        Task<VideoModelProvider?> GetByIdAsync(string id);
        Task<List<VideoModelProvider>> GetAsync();
        Task InsertAsync(VideoModelProvider provider);
        Task UpdateAsync(VideoModelProvider provider);
        Task DeleteAsync(string id);
    }
}
