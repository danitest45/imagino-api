using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Imagino.Api.DTOs;
using Imagino.Api.Models.Image;
using Imagino.Api.Repositories.Image;
using Imagino.Api.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Imagino.Api.Repository.Image
{
    public class ImageModelRepository : IImageModelRepository
    {
        private readonly IMongoCollection<ImageModel> _collection;

        public ImageModelRepository(IMongoClient mongoClient, IOptions<ImageGeneratorSettings> settings)
        {
            var database = mongoClient.GetDatabase(settings.Value.MongoDatabase);
            _collection = database.GetCollection<ImageModel>("image_models");
        }

        public async Task<ImageModel?> GetByIdAsync(string id) =>
            await _collection.Find(m => m.Id == id).FirstOrDefaultAsync();

        public async Task<ImageModel?> GetBySlugAsync(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return null;
            }

            var pattern = $"^{Regex.Escape(slug.Trim())}$";
            var filter = Builders<ImageModel>.Filter.Regex(
                model => model.Slug,
                new BsonRegularExpression(pattern, "i"));

            return await _collection
                .Find(filter)
                .FirstOrDefaultAsync();
        }

        public async Task<List<ImageModel>> GetAsync(ImageModelStatus? status = null, ImageModelVisibility? visibility = null, IEnumerable<string>? ids = null)
        {
            var filters = new List<FilterDefinition<ImageModel>>();

            if (status.HasValue)
            {
                filters.Add(Builders<ImageModel>.Filter.Eq(m => m.Status, status.Value));
            }

            if (visibility.HasValue)
            {
                filters.Add(Builders<ImageModel>.Filter.Eq(m => m.Visibility, visibility.Value));
            }

            if (ids != null)
            {
                filters.Add(Builders<ImageModel>.Filter.In(m => m.Id, ids));
            }

            var filter = filters.Count > 0
                ? Builders<ImageModel>.Filter.And(filters)
                : Builders<ImageModel>.Filter.Empty;

            return await _collection.Find(filter).ToListAsync();
        }

        public async Task<PagedResult<ImageModel>> GetPagedAsync(ImageModelStatus? status, ImageModelVisibility? visibility, int page, int pageSize)
        {
            var filters = new List<FilterDefinition<ImageModel>>();

            if (status.HasValue)
            {
                filters.Add(Builders<ImageModel>.Filter.Eq(m => m.Status, status.Value));
            }

            if (visibility.HasValue)
            {
                filters.Add(Builders<ImageModel>.Filter.Eq(m => m.Visibility, visibility.Value));
            }

            var filter = filters.Count > 0
                ? Builders<ImageModel>.Filter.And(filters)
                : Builders<ImageModel>.Filter.Empty;

            var total = await _collection.CountDocumentsAsync(filter);

            var models = await _collection.Find(filter)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();

            return new PagedResult<ImageModel>
            {
                Items = models,
                Total = total
            };
        }

        public async Task InsertAsync(ImageModel model)
        {
            await _collection.InsertOneAsync(model);
        }

        public async Task UpdateAsync(ImageModel model)
        {
            await _collection.ReplaceOneAsync(m => m.Id == model.Id, model);
        }

        public async Task DeleteAsync(string id)
        {
            await _collection.DeleteOneAsync(m => m.Id == id);
        }

        public async Task SetDefaultVersionAsync(string modelId, string versionId)
        {
            var update = Builders<ImageModel>.Update
                .Set(m => m.DefaultVersionId, versionId)
                .Set(m => m.UpdatedAt, DateTime.UtcNow);

            await _collection.UpdateOneAsync(m => m.Id == modelId, update);
        }
    }
}
