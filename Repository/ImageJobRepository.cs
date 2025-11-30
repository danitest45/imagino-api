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
            return await _collection.Find(_ => true)
                                    .SortByDescending(job => job.CreatedAt)
                                    .Limit(limit)
                                    .ToListAsync();
        }

    }
}