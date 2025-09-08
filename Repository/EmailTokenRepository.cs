using System.Security.Cryptography;
using System.Text;
using Imagino.Api.Models;
using Imagino.Api.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Imagino.Api.Repository
{
    public class EmailTokenRepository : IEmailTokenRepository
    {
        private readonly IMongoCollection<EmailToken> _collection;

        public EmailTokenRepository(IOptions<ImageGeneratorSettings> settings)
        {
            var client = new MongoClient(settings.Value.MongoConnection);
            var db = client.GetDatabase(settings.Value.MongoDatabase);
            _collection = db.GetCollection<EmailToken>("email_tokens");
            var index = Builders<EmailToken>.IndexKeys
                .Ascending(x => x.UserId)
                .Ascending(x => x.Purpose)
                .Ascending(x => x.ExpiresAt);
            _collection.Indexes.CreateOne(new CreateIndexModel<EmailToken>(index));
        }

        private static string Hash(string raw)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
            return Convert.ToBase64String(bytes);
        }

        public async Task CreateAsync(string userId, string purpose, string rawToken, TimeSpan ttl, string? ip = null)
        {
            var token = new EmailToken
            {
                UserId = userId,
                Purpose = purpose,
                TokenHash = Hash(rawToken),
                ExpiresAt = DateTime.UtcNow.Add(ttl),
                CreatedAt = DateTime.UtcNow,
                CreatedIp = ip
            };
            await _collection.InsertOneAsync(token);
        }

        public async Task<EmailToken?> GetActiveByRawTokenAsync(string purpose, string rawToken)
        {
            var hash = Hash(rawToken);
            var now = DateTime.UtcNow;
            var filter = Builders<EmailToken>.Filter.And(
                Builders<EmailToken>.Filter.Eq(x => x.Purpose, purpose),
                Builders<EmailToken>.Filter.Eq(x => x.TokenHash, hash),
                Builders<EmailToken>.Filter.Gte(x => x.ExpiresAt, now),
                Builders<EmailToken>.Filter.Eq(x => x.ConsumedAt, null));
            return await _collection.Find(filter).FirstOrDefaultAsync();
        }

        public async Task ConsumeAsync(string id)
        {
            var update = Builders<EmailToken>.Update.Set(x => x.ConsumedAt, DateTime.UtcNow);
            await _collection.UpdateOneAsync(x => x.Id == id, update);
        }

        public async Task<int> CountByUserInWindowAsync(string userId, string purpose, TimeSpan window)
        {
            var since = DateTime.UtcNow - window;
            var filter = Builders<EmailToken>.Filter.And(
                Builders<EmailToken>.Filter.Eq(x => x.UserId, userId),
                Builders<EmailToken>.Filter.Eq(x => x.Purpose, purpose),
                Builders<EmailToken>.Filter.Gte(x => x.CreatedAt, since));
            return (int)await _collection.CountDocumentsAsync(filter);
        }

        public async Task InvalidateByUserAsync(string userId, string purpose)
        {
            var filter = Builders<EmailToken>.Filter.And(
                Builders<EmailToken>.Filter.Eq(x => x.UserId, userId),
                Builders<EmailToken>.Filter.Eq(x => x.Purpose, purpose),
                Builders<EmailToken>.Filter.Eq(x => x.ConsumedAt, null));
            var update = Builders<EmailToken>.Update.Set(x => x.ConsumedAt, DateTime.UtcNow);
            await _collection.UpdateManyAsync(filter, update);
        }
    }
}
