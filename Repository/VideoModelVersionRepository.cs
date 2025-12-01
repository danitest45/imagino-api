using System.Collections.Generic;
using Imagino.Api.Models.Video;
using Imagino.Api.Repositories.Video;
using Imagino.Api.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Imagino.Api.Repository
{
    public class VideoModelVersionRepository : IVideoModelVersionRepository
    {
        private readonly IMongoCollection<VideoModelVersion> _collection;

        public VideoModelVersionRepository(IMongoClient mongoClient, IOptions<ImageGeneratorSettings> settings)
        {
            var database = mongoClient.GetDatabase(settings.Value.MongoDatabase);
            _collection = database.GetCollection<VideoModelVersion>("video_model_versions");
        }

        public async Task<VideoModelVersion?> GetByIdAsync(string id) =>
            await _collection.Find(v => v.Id == id).FirstOrDefaultAsync();

        public async Task<VideoModelVersion?> GetByModelAndTagAsync(string modelId, string versionTag) =>
            await _collection.Find(v => v.ModelId == modelId && v.VersionTag == versionTag).FirstOrDefaultAsync();

        public async Task<List<VideoModelVersion>> GetByModelIdAsync(string modelId, VideoModelVersionStatus? status = null)
        {
            var filters = new List<FilterDefinition<VideoModelVersion>>
            {
                Builders<VideoModelVersion>.Filter.Eq(v => v.ModelId, modelId)
            };

            if (status.HasValue)
            {
                filters.Add(Builders<VideoModelVersion>.Filter.Eq(v => v.Status, status.Value));
            }

            var filter = filters.Count > 1
                ? Builders<VideoModelVersion>.Filter.And(filters)
                : filters[0];

            return await _collection.Find(filter).ToListAsync();
        }

        public async Task InsertAsync(VideoModelVersion version)
        {
            await _collection.InsertOneAsync(version);
        }

        public async Task UpdateAsync(VideoModelVersion version)
        {
            await _collection.ReplaceOneAsync(v => v.Id == version.Id, version);
        }

        public async Task DeleteAsync(string id)
        {
            await _collection.DeleteOneAsync(v => v.Id == id);
        }
    }
}
