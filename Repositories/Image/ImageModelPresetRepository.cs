using System.Collections.Generic;
using Imagino.Api.Models.Image;
using Imagino.Api.Repositories.Image;
using Imagino.Api.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Imagino.Api.Repository.Image
{
    public class ImageModelPresetRepository : IImageModelPresetRepository
    {
        private readonly IMongoCollection<ImageModelPreset> _collection;

        public ImageModelPresetRepository(IMongoClient mongoClient, IOptions<ImageGeneratorSettings> settings)
        {
            var database = mongoClient.GetDatabase(settings.Value.MongoDatabase);
            _collection = database.GetCollection<ImageModelPreset>("image_model_presets");
        }

        public async Task<ImageModelPreset?> GetByIdAsync(string id) =>
            await _collection.Find(p => p.Id == id).FirstOrDefaultAsync();

        public async Task<ImageModelPreset?> GetBySlugAsync(string modelId, string slug) =>
            await _collection.Find(p => p.ModelId == modelId && p.Slug == slug).FirstOrDefaultAsync();

        public async Task<List<ImageModelPreset>> GetByModelIdAsync(string modelId, ImageModelPresetStatus? status = null, ImageModelVisibility? visibility = null)
        {
            var filters = new List<FilterDefinition<ImageModelPreset>>
            {
                Builders<ImageModelPreset>.Filter.Eq(p => p.ModelId, modelId)
            };

            if (status.HasValue)
            {
                filters.Add(Builders<ImageModelPreset>.Filter.Eq(p => p.Status, status.Value));
            }

            if (visibility.HasValue)
            {
                filters.Add(Builders<ImageModelPreset>.Filter.Eq(p => p.Visibility, visibility.Value));
            }

            var filter = filters.Count > 1
                ? Builders<ImageModelPreset>.Filter.And(filters)
                : filters[0];

            return await _collection.Find(filter).ToListAsync();
        }

        public async Task InsertAsync(ImageModelPreset preset)
        {
            await _collection.InsertOneAsync(preset);
        }

        public async Task UpdateAsync(ImageModelPreset preset)
        {
            await _collection.ReplaceOneAsync(p => p.Id == preset.Id, preset);
        }

        public async Task DeleteAsync(string id)
        {
            await _collection.DeleteOneAsync(p => p.Id == id);
        }
    }
}
