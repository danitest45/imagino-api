using Imagino.Api.Models;

namespace Imagino.Api.Repository
{
    public interface IImageJobRepository
    {
        Task InsertAsync(ImageJob job);
        Task<ImageJob?> GetByProviderJobIdAsync(string providerId);
        Task<bool> TryClaimWebhookAsync(ImageJob job, string lease);
        Task<bool> CompleteWebhookAsync(string id, string lease, string url);
        Task ReleaseWebhookAsync(string id, string lease);
        Task<ImageJob> GetByJobIdAsync(string jobId);
        Task UpdateAsync(ImageJob job);
        Task<List<ImageJob>> GetByUserIdAsync(string userId);
        Task<List<ImageJob>> GetLatestAsync(int limit);
    }
}