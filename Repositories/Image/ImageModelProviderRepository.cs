using System.Collections.Generic;
using Imagino.Api.Models.Image;
using Imagino.Api.Repositories.Image;
using Imagino.Api.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Imagino.Api.Repository.Image
{
    public class ImageModelProviderRepository : IImageModelProviderRepository
    {
        private readonly IMongoCollection<ImageModelProvider> _collection;

        public ImageModelProviderRepository(IMongoClient mongoClient, IOptions<ImageGeneratorSettings> settings)
        {
            var database = mongoClient.GetDatabase(settings.Value.MongoDatabase);
            _collection = database.GetCollection<ImageModelProvider>("image_model_providers");
        }

        public async Task<ImageModelProvider?> GetByIdAsync(string id) =>
            await _collection.Find(p => p.Id == id).FirstOrDefaultAsync();

        public async Task<ImageModelProvider?> GetByNameAsync(string name) =>
            await _collection.Find(p => p.Name.ToLower() == name.ToLower()).FirstOrDefaultAsync();

        public async Task<List<ImageModelProvider>> GetAsync(ImageModelProviderStatus? status = null)
        {
            var filter = status.HasValue
                ? Builders<ImageModelProvider>.Filter.Eq(p => p.Status, status.Value)
                : Builders<ImageModelProvider>.Filter.Empty;

            return await _collection.Find(filter).ToListAsync();
        }

        public async Task InsertAsync(ImageModelProvider provider)
        {
            await _collection.InsertOneAsync(provider);
        }

        public async Task UpdateAsync(ImageModelProvider provider)
        {
            await _collection.ReplaceOneAsync(p => p.Id == provider.Id, provider);
        }

        public async Task DeleteAsync(string id)
        {
            await _collection.DeleteOneAsync(p => p.Id == id);
        }
    }
}
