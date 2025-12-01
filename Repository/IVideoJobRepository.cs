using Imagino.Api.Models;

namespace Imagino.Api.Repository
{
    public interface IVideoJobRepository
    {
        Task InsertAsync(VideoJob job);
        Task<VideoJob?> GetByJobIdAsync(string jobId);
        Task UpdateAsync(VideoJob job);
    }
}
