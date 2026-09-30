using Imagino.Api.Models;
using Imagino.Api.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Imagino.Api.Repository
{
    public class ImageJobRepository : IImageJobRepository
    {
        private readonly IMongoCollection<ImageJob> _collection;

        public ImageJobRepository(IMongoClient mongoClient, IOptions<ImageGeneratorSettings> config)
        {
            var db = mongoClient.GetDatabase(config.Value.MongoDatabase);
            _collection = db.GetCollection<ImageJob>(config.Value.JobsCollection);
        }

        public async Task<ImageJob?> GetByProviderJobIdAsync(string providerId) =>
            await _collection.Find(j => j.ProviderJobId == providerId).FirstOrDefaultAsync();

        public static FilterDefinition<ImageJob> WebhookClaimFilter(ImageJob job, DateTime now) =>
            Builders<ImageJob>.Filter.Eq(j => j.Id, job.Id) &
            Builders<ImageJob>.Filter.Eq(j => j.UserId, job.UserId) &
            Builders<ImageJob>.Filter.Eq(j => j.ProviderJobId, job.ProviderJobId) &
            Builders<ImageJob>.Filter.Eq(j => j.CallbackProvider, job.CallbackProvider) &
            Builders<ImageJob>.Filter.Eq(j => j.TokenConsumed, true) &
            Builders<ImageJob>.Filter.In(j => j.Status, new[] { ImageJobStatus.Queued, ImageJobStatus.Running, ImageJobStatus.Starting, ImageJobStatus.Processing }) &
            (Builders<ImageJob>.Filter.Eq(j => j.WebhookLease, null) | Builders<ImageJob>.Filter.Lt(j => j.WebhookLeaseExpiresAt, now));

        public async Task<bool> TryClaimWebhookAsync(ImageJob job, string lease)
        {
            var now = DateTime.UtcNow;
            var update = Builders<ImageJob>.Update.Set(j => j.WebhookLease, lease).Set(j => j.WebhookLeaseExpiresAt, now.AddMinutes(3));
            var result = await _collection.UpdateOneAsync(WebhookClaimFilter(job, now), update);
            return result.ModifiedCount == 1;
        }

        public async Task<bool> CompleteWebhookAsync(string id, string lease, string url)
        {
            var result = await _collection.UpdateOneAsync(j => j.Id == id && j.WebhookLease == lease &&
                j.WebhookLeaseExpiresAt > DateTime.UtcNow,
                Builders<ImageJob>.Update.Set(j => j.Status, ImageJobStatus.Completed)
                    .Set(j => j.ImageUrls, new List<string> { url }).Set(j => j.UpdatedAt, DateTime.UtcNow)
                    .Unset(j => j.WebhookLease).Unset(j => j.WebhookLeaseExpiresAt));
            return result.ModifiedCount == 1;
        }

        public async Task ReleaseWebhookAsync(string id, string lease) =>
            await _collection.UpdateOneAsync(j => j.Id == id && j.WebhookLease == lease,
                Builders<ImageJob>.Update.Unset(j => j.WebhookLease).Unset(j => j.WebhookLeaseExpiresAt));

        public async Task InsertAsync(ImageJob job)
        {
            await _collection.InsertOneAsync(job);
        }

        public async Task<ImageJob> GetByJobIdAsync(string jobId)
        {
            var filter = Builders<ImageJob>.Filter.Or(
                Builders<ImageJob>.Filter.Eq(job => job.JobId, jobId),
                Builders<ImageJob>.Filter.Eq(job => job.ProviderJobId, jobId),
                Builders<ImageJob>.Filter.Eq(job => job.Id, jobId)
            );

            return await _collection
                .Find(filter)
                .FirstOrDefaultAsync();
        }

        public async Task UpdateAsync(ImageJob job)
        {
            var filter = Builders<ImageJob>.Filter.Or(
                Builders<ImageJob>.Filter.Eq(j => j.Id, job.Id),
                Builders<ImageJob>.Filter.Eq(j => j.JobId, job.JobId),
                Builders<ImageJob>.Filter.Eq(j => j.ProviderJobId, job.ProviderJobId)
            );
            await _collection.ReplaceOneAsync(filter, job);
        }
        public async Task<List<ImageJob>> GetByUserIdAsync(string userId)
        {
            return await _collection.Find(job => job.UserId == userId)
                                    .SortByDescending(job => job.CreatedAt)
                                    .ToListAsync();
        }

        public async Task<List<ImageJob>> GetLatestAsync(int limit)
        {
            return await _collection.Find(job => job.IsPublic)
                                    .SortByDescending(job => job.CreatedAt)
                                    .Limit(limit)
                                    .ToListAsync();
        }

    }
}
