using Imagino.Api.Models;
using Imagino.Api.Repository;
using Imagino.Api.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Imagino.Api.Repository
{
    public class UserRepository : IUserRepository
    {
        private readonly IMongoCollection<User> _collection;

        public UserRepository(IOptions<ImageGeneratorSettings> settings)
        {
            var client = new MongoClient(settings.Value.MongoConnection);
            var database = client.GetDatabase(settings.Value.MongoDatabase);
            _collection = database.GetCollection<User>("Users");
        }

        public async Task<User> GetByEmailAsync(string email) =>
            await _collection.Find(u => u.Email.ToLower() == email.ToLower()).FirstOrDefaultAsync();

        public async Task<User> GetByGoogleIdAsync(string googleId) =>
            await _collection.Find(u => u.GoogleId == googleId).FirstOrDefaultAsync();

        public async Task<User?> GetByUsernameAsync(string username) =>
            await _collection.Find(u => u.Username.ToLower() == username.ToLower()).FirstOrDefaultAsync();

        public async Task<User?> GetByIdAsync(string id) =>
            await _collection.Find(u => u.Id == id).FirstOrDefaultAsync();

        public async Task<IEnumerable<User>> GetAllAsync() =>
            await _collection.Find(_ => true).ToListAsync();

        public async Task CreateAsync(User user) =>
            await _collection.InsertOneAsync(user);

        public async Task UpdateAsync(User user) =>
            await _collection.ReplaceOneAsync(u => u.Id == user.Id, user);

        public async Task DeleteAsync(string id) =>
            await _collection.DeleteOneAsync(u => u.Id == id);

        public async Task<bool> DecrementCreditsAsync(string userId, int amount)
        {
            var filter = Builders<User>.Filter.And(
                Builders<User>.Filter.Eq(u => u.Id, userId),
                Builders<User>.Filter.Gte(u => u.Credits, amount));
            var update = Builders<User>.Update.Inc(u => u.Credits, -amount);

            var result = await _collection.UpdateOneAsync(filter, update);
            return result.ModifiedCount == 1;
        }

        public async Task<bool> IncrementCreditsAsync(string userId, int amount)
        {
            var update = Builders<User>.Update.Inc(u => u.Credits, amount);
            var result = await _collection.UpdateOneAsync(u => u.Id == userId, update);
            return result.ModifiedCount == 1;
        }

        public async Task<int?> GetCreditsAsync(string userId)
        {
            var filter = Builders<User>.Filter.Eq(u => u.Id, userId);
            var result = await _collection.Find(filter).Project(u => (int?)u.Credits).FirstOrDefaultAsync();
            return result;
        }
    }
}
