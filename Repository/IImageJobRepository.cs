using Imagino.Api.Models;

namespace Imagino.Api.Repository
{
    public interface IImageJobRepository
    {
        Task InsertAsync(ImageJob job);
        Task<ImageJob> GetByJobIdAsync(string jobId);
        Task UpdateAsync(ImageJob job);
        Task<List<ImageJob>> GetByUserIdAsync(string userId);
        Task<List<ImageJob>> GetLatestAsync(int limit);
    }
}