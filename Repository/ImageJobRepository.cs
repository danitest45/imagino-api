using Imagino.Api.DTOs;
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

        public async Task InsertAsync(ImageJob job)
        {
            await _collection.InsertOneAsync(job);
        }

        public async Task<ImageJob> GetByJobIdAsync(string jobId)
        {
            return await _collection
                .Find(job => job.JobId == jobId)
                .FirstOrDefaultAsync();
        }

        public async Task UpdateAsync(ImageJob job)
        {
            var filter = Builders<ImageJob>.Filter.Eq(j => j.JobId, job.JobId);
            await _collection.ReplaceOneAsync(filter, job);
        }

        public async Task<PagedResult<ImageJob>> GetByUserIdAsync(string userId, int page, int pageSize)
        {
            var filter = Builders<ImageJob>.Filter.Eq(job => job.UserId, userId);
            var total = await _collection.CountDocumentsAsync(filter);

            var items = await _collection.Find(filter)
                .SortByDescending(job => job.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();

            return new PagedResult<ImageJob>
            {
                Items = items,
                Total = total
            };
        }

        public async Task<PagedResult<ImageJob>> GetLatestAsync(int page, int pageSize)
        {
            var filter = Builders<ImageJob>.Filter.Empty;
            var total = await _collection.CountDocumentsAsync(filter);

            var items = await _collection.Find(filter)
                .SortByDescending(job => job.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();

            return new PagedResult<ImageJob>
            {
                Items = items,
                Total = total
            };
        }
    }
}
