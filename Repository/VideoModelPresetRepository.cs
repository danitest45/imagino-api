using System.Collections.Generic;
using Imagino.Api.Models.Video;
using Imagino.Api.Repositories.Video;
using Imagino.Api.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Imagino.Api.Repository
{
    public class VideoModelPresetRepository : IVideoModelPresetRepository
    {
        private readonly IMongoCollection<VideoModelPreset> _collection;

        public VideoModelPresetRepository(IMongoClient mongoClient, IOptions<ImageGeneratorSettings> settings)
        {
            var database = mongoClient.GetDatabase(settings.Value.MongoDatabase);
            _collection = database.GetCollection<VideoModelPreset>("video_model_presets");
        }

        public async Task<VideoModelPreset?> GetByIdAsync(string id) =>
            await _collection.Find(p => p.Id == id).FirstOrDefaultAsync();

        public async Task<VideoModelPreset?> GetBySlugAsync(string slug) =>
            await _collection.Find(p => p.Slug == slug).FirstOrDefaultAsync();

        public async Task<List<VideoModelPreset>> GetByModelIdAsync(string modelId, VideoModelPresetStatus? status = null)
        {
            var filters = new List<FilterDefinition<VideoModelPreset>>
            {
                Builders<VideoModelPreset>.Filter.Eq(p => p.ModelId, modelId)
            };

            if (status.HasValue)
            {
                filters.Add(Builders<VideoModelPreset>.Filter.Eq(p => p.Status, status.Value));
            }

            var filter = filters.Count > 1
                ? Builders<VideoModelPreset>.Filter.And(filters)
                : filters[0];

            return await _collection.Find(filter).ToListAsync();
        }

        public async Task InsertAsync(VideoModelPreset preset)
        {
            await _collection.InsertOneAsync(preset);
        }

        public async Task UpdateAsync(VideoModelPreset preset)
        {
            await _collection.ReplaceOneAsync(p => p.Id == preset.Id, preset);
        }

        public async Task DeleteAsync(string id)
        {
            await _collection.DeleteOneAsync(p => p.Id == id);
        }
    }
}
