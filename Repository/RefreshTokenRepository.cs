using Imagino.Api.Models;
using Imagino.Api.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System.Threading.Tasks;

namespace Imagino.Api.Repository
{
    public class RefreshTokenRepository : IRefreshTokenRepository
    {
        private readonly IMongoCollection<RefreshToken> _collection;

        public RefreshTokenRepository(IOptions<ImageGeneratorSettings> settings)
        {
            var client = new MongoClient(settings.Value.MongoConnection);
            var database = client.GetDatabase(settings.Value.MongoDatabase);
            _collection = database.GetCollection<RefreshToken>("RefreshTokens");
        }

        public async Task CreateAsync(RefreshToken token)
        {
            await _collection.InsertOneAsync(ForStorage(token));
        }

        public static RefreshToken ForStorage(RefreshToken token)
        {
            if (string.IsNullOrEmpty(token.Token)) throw new ArgumentException("Refresh token is required.");
            return new RefreshToken { Id = token.Id, UserId = token.UserId, ExpiresAt = token.ExpiresAt, CreatedAt = DateTime.UtcNow,
                TokenHash = Imagino.Api.Security.TokenSecurity.Hash(token.Token) };
        }

        public static FilterDefinition<RefreshToken> TokenFilter(string token) =>
            Builders<RefreshToken>.Filter.Eq(t => t.TokenHash, Imagino.Api.Security.TokenSecurity.Hash(token)) |
            (Builders<RefreshToken>.Filter.Eq(t => t.TokenHash, null) & Builders<RefreshToken>.Filter.Eq(t => t.Token, token));

        public async Task<RefreshToken?> ConsumeAsync(string token) =>
            await _collection.FindOneAndDeleteAsync(TokenFilter(token) &
                Builders<RefreshToken>.Filter.Gt(t => t.ExpiresAt, DateTime.UtcNow));

        public async Task<RefreshToken?> GetByTokenAsync(string token) =>
            await _collection.Find(TokenFilter(token)).FirstOrDefaultAsync();

        public async Task DeleteAsync(string token) =>
            await _collection.DeleteOneAsync(TokenFilter(token));

        public async Task DeleteByUserIdAsync(string userId) =>
            await _collection.DeleteManyAsync(t => t.UserId == userId);
    }
}
