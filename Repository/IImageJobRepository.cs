using Imagino.Api.Models;

namespace Imagino.Api.Repository
{
    public interface IImageJobRepository
    {
        Task InsertAsync(ImageJob job);
        Task<ImageJob> GetByJobIdAsync(string jobId);
        Task UpdateAsync(ImageJob job);
    }
}