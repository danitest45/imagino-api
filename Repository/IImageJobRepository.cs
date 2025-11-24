using Imagino.Api.DTOs;
using Imagino.Api.Models;

namespace Imagino.Api.Repository
{
    public interface IImageJobRepository
    {
        Task InsertAsync(ImageJob job);
        Task<ImageJob> GetByJobIdAsync(string jobId);
        Task UpdateAsync(ImageJob job);
        Task<PagedResult<ImageJob>> GetByUserIdAsync(string userId, int page, int pageSize);
        Task<PagedResult<ImageJob>> GetLatestAsync(int page, int pageSize);
    }
}
