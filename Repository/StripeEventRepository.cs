using System.Threading.Tasks;
using Imagino.Api.Models;
using Imagino.Api.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Imagino.Api.Repository
{
    public class StripeEventRepository : IStripeEventRepository
    {
        private readonly IMongoCollection<StripeEventRecord> _collection;

        public StripeEventRepository(IOptions<ImageGeneratorSettings> settings)
        {
            var client = new MongoClient(settings.Value.MongoConnection);
            var database = client.GetDatabase(settings.Value.MongoDatabase);
            _collection = database.GetCollection<StripeEventRecord>("stripe_events");
        }

        public async Task<bool> ExistsAsync(string eventId)
        {
            var count = await _collection.CountDocumentsAsync(e => e.EventId == eventId);
            return count > 0;
        }

        public Task CreateAsync(StripeEventRecord record) => _collection.InsertOneAsync(record);
    }
}
