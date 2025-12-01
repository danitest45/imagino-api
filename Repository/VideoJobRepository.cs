using Imagino.Api.Models;
using Imagino.Api.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Imagino.Api.Repository
{
    public class VideoJobRepository : IVideoJobRepository
    {
        private readonly IMongoCollection<VideoJob> _collection;

        public VideoJobRepository(IMongoClient mongoClient, IOptions<ImageGeneratorSettings> config)
        {
            var db = mongoClient.GetDatabase(config.Value.MongoDatabase);
            var collectionName = string.IsNullOrWhiteSpace(config.Value.VideoJobsCollection)
                ? "video_jobs"
                : config.Value.VideoJobsCollection;
            _collection = db.GetCollection<VideoJob>(collectionName);
        }

        public async Task InsertAsync(VideoJob job)
        {
            await _collection.InsertOneAsync(job);
        }

        public async Task<VideoJob?> GetByJobIdAsync(string jobId)
        {
            var filter = Builders<VideoJob>.Filter.Or(
                Builders<VideoJob>.Filter.Eq(job => job.JobId, jobId),
                Builders<VideoJob>.Filter.Eq(job => job.ProviderJobId, jobId),
                Builders<VideoJob>.Filter.Eq(job => job.Id, jobId)
            );

            return await _collection
                .Find(filter)
                .FirstOrDefaultAsync();
        }

        public async Task UpdateAsync(VideoJob job)
        {
            var filter = Builders<VideoJob>.Filter.Or(
                Builders<VideoJob>.Filter.Eq(j => j.Id, job.Id),
                Builders<VideoJob>.Filter.Eq(j => j.JobId, job.JobId),
                Builders<VideoJob>.Filter.Eq(j => j.ProviderJobId, job.ProviderJobId)
            );
            await _collection.ReplaceOneAsync(filter, job);
        }
    }
}
