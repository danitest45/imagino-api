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

        public async Task CreateAsync(RefreshToken token) =>
            await _collection.InsertOneAsync(token);

        public async Task<RefreshToken?> GetByTokenAsync(string token) =>
            await _collection.Find(t => t.Token == token).FirstOrDefaultAsync();

        public async Task DeleteAsync(string token) =>
            await _collection.DeleteOneAsync(t => t.Token == token);

        public async Task DeleteByUserIdAsync(string userId) =>
            await _collection.DeleteManyAsync(t => t.UserId == userId);
    }
}
