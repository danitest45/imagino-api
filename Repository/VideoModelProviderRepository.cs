using System.Collections.Generic;
using Imagino.Api.Models.Video;
using Imagino.Api.Repositories.Video;
using Imagino.Api.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Imagino.Api.Repository
{
    public class VideoModelProviderRepository : IVideoModelProviderRepository
    {
        private readonly IMongoCollection<VideoModelProvider> _collection;

        public VideoModelProviderRepository(IMongoClient mongoClient, IOptions<ImageGeneratorSettings> settings)
        {
            var database = mongoClient.GetDatabase(settings.Value.MongoDatabase);
            _collection = database.GetCollection<VideoModelProvider>("video_model_providers");
        }

        public async Task<VideoModelProvider?> GetByIdAsync(string id) =>
            await _collection.Find(p => p.Id == id).FirstOrDefaultAsync();

        public async Task<List<VideoModelProvider>> GetAsync()
        {
            return await _collection.Find(Builders<VideoModelProvider>.Filter.Empty).ToListAsync();
        }

        public async Task InsertAsync(VideoModelProvider provider)
        {
            await _collection.InsertOneAsync(provider);
        }

        public async Task UpdateAsync(VideoModelProvider provider)
        {
            await _collection.ReplaceOneAsync(p => p.Id == provider.Id, provider);
        }

        public async Task DeleteAsync(string id)
        {
            await _collection.DeleteOneAsync(p => p.Id == id);
        }
    }
}
