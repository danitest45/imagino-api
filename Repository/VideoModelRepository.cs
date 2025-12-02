using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Imagino.Api.Models.Video;
using Imagino.Api.Repositories.Video;
using Imagino.Api.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Imagino.Api.Repository
{
    public class VideoModelRepository : IVideoModelRepository
    {
        private readonly IMongoCollection<VideoModel> _collection;

        public VideoModelRepository(IMongoClient mongoClient, IOptions<ImageGeneratorSettings> settings)
        {
            var database = mongoClient.GetDatabase(settings.Value.MongoDatabase);
            _collection = database.GetCollection<VideoModel>("video_models");
        }

        public async Task<VideoModel?> GetByIdAsync(string id) =>
            await _collection.Find(m => m.Id == id).FirstOrDefaultAsync();

        public async Task<VideoModel?> GetBySlugAsync(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return null;
            }

            var pattern = $"^{Regex.Escape(slug.Trim())}$";
            var filter = Builders<VideoModel>.Filter.Regex(
                model => model.Slug,
                new BsonRegularExpression(pattern, "i"));

            return await _collection
                .Find(filter)
                .FirstOrDefaultAsync();
        }

        public async Task<List<VideoModel>> GetAsync(VideoModelStatus? status = null, VideoModelVisibility? visibility = null, IEnumerable<string>? ids = null)
        {
            var filters = new List<FilterDefinition<VideoModel>>();

            if (status.HasValue)
            {
                filters.Add(Builders<VideoModel>.Filter.Eq(m => m.Status, status.Value));
            }

            if (visibility.HasValue)
            {
                filters.Add(Builders<VideoModel>.Filter.Eq(m => m.Visibility, visibility.Value));
            }

            if (ids != null)
            {
                filters.Add(Builders<VideoModel>.Filter.In(m => m.Id, ids));
            }

            var filter = filters.Count > 0
                ? Builders<VideoModel>.Filter.And(filters)
                : Builders<VideoModel>.Filter.Empty;

            return await _collection.Find(filter).ToListAsync();
        }

        public async Task InsertAsync(VideoModel model)
        {
            await _collection.InsertOneAsync(model);
        }

        public async Task UpdateAsync(VideoModel model)
        {
            await _collection.ReplaceOneAsync(m => m.Id == model.Id, model);
        }

        public async Task DeleteAsync(string id)
        {
            await _collection.DeleteOneAsync(m => m.Id == id);
        }

        public async Task SetDefaultVersionAsync(string modelId, string versionId)
        {
            var update = Builders<VideoModel>.Update
                .Set(m => m.DefaultVersionId, versionId)
                .Set(m => m.UpdatedAt, DateTime.UtcNow);

            await _collection.UpdateOneAsync(m => m.Id == modelId, update);
        }
    }
}
