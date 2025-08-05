using Imagino.Api.Models;
using Imagino.Api.Repository;
using Imagino.Api.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
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

        public async Task CreateAsync(User user) =>
            await _collection.InsertOneAsync(user);
    }
}
