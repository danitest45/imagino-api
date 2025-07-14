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

    }
}