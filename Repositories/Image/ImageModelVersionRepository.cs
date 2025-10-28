using System.Collections.Generic;
using Imagino.Api.Models.Image;
using Imagino.Api.Repositories.Image;
using Imagino.Api.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Imagino.Api.Repository.Image
{
    public class ImageModelVersionRepository : IImageModelVersionRepository
    {
        private readonly IMongoCollection<ImageModelVersion> _collection;

        public ImageModelVersionRepository(IMongoClient mongoClient, IOptions<ImageGeneratorSettings> settings)
        {
            var database = mongoClient.GetDatabase(settings.Value.MongoDatabase);
            _collection = database.GetCollection<ImageModelVersion>("image_model_versions");
        }

        public async Task<ImageModelVersion?> GetByIdAsync(string id) =>
            await _collection.Find(v => v.Id == id).FirstOrDefaultAsync();

        public async Task<ImageModelVersion?> GetByModelAndTagAsync(string modelId, string versionTag) =>
            await _collection.Find(v => v.ModelId == modelId && v.VersionTag == versionTag).FirstOrDefaultAsync();

        public async Task<List<ImageModelVersion>> GetByModelIdAsync(string modelId, ImageModelVersionStatus? status = null)
        {
            var filters = new List<FilterDefinition<ImageModelVersion>>
            {
                Builders<ImageModelVersion>.Filter.Eq(v => v.ModelId, modelId)
            };

            if (status.HasValue)
            {
                filters.Add(Builders<ImageModelVersion>.Filter.Eq(v => v.Status, status.Value));
            }

            var filter = filters.Count > 1
                ? Builders<ImageModelVersion>.Filter.And(filters)
                : filters[0];

            return await _collection.Find(filter).ToListAsync();
        }

        public async Task InsertAsync(ImageModelVersion version)
        {
            await _collection.InsertOneAsync(version);
        }

        public async Task UpdateAsync(ImageModelVersion version)
        {
            await _collection.ReplaceOneAsync(v => v.Id == version.Id, version);
        }

        public async Task DeleteAsync(string id)
        {
            await _collection.DeleteOneAsync(v => v.Id == id);
        }
    }
}
