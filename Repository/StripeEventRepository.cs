using Imagino.Api.Models;
using Imagino.Api.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
namespace Imagino.Api.Repository;
public class StripeEventRepository : IStripeEventRepository
{
    private readonly IMongoCollection<StripeEventRecord> collection;
    public StripeEventRepository(IMongoCollection<StripeEventRecord> collection) { this.collection = collection; }
    public StripeEventRepository(IOptions<ImageGeneratorSettings> settings) {
        collection = new MongoClient(settings.Value.MongoConnection)
            .GetDatabase(settings.Value.MongoDatabase).GetCollection<StripeEventRecord>("stripe_events");
    }
    public async Task<StripeClaim> TryClaimAsync(StripeEventRecord record)
    {
        using (var cursor = await collection.Indexes.ListAsync())
        {
            var indexes = await cursor.ToListAsync();
            if (!indexes.Any(i => i.GetValue("unique", false).AsBoolean &&
                i["key"].AsBsonDocument.ElementCount == 1 && i["key"].AsBsonDocument.Contains("EventId") &&
                !i.Contains("partialFilterExpression") && !i.GetValue("sparse", false).AsBoolean))
                throw new InvalidOperationException("Stripe EventId unique index is required");
        }
        // _id is also EventId: insertion is atomic; the unique EventId index also covers legacy records.
        record.Id = record.EventId;
        record.LeaseUntil = DateTime.UtcNow.AddMinutes(5);
        try { await collection.InsertOneAsync(record); return StripeClaim.Acquired; }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey) { }
        var existing = await collection.Find(e => e.EventId == record.EventId)
            .Project(e => new { e.Status, e.ClaimId }).FirstOrDefaultAsync();
        if (existing is not null && (existing.Status == "completed" || existing.ClaimId == null)) return StripeClaim.Completed;
        var f = Builders<StripeEventRecord>.Filter;
        var retry = f.Eq(e => e.EventId, record.EventId) &
            (f.Eq(e => e.Status, "failed") | f.Lte(e => e.LeaseUntil, DateTime.UtcNow)) & f.Ne(e => e.Status, "completed");
        var result = await collection.UpdateOneAsync(retry, Builders<StripeEventRecord>.Update
            .Set(e => e.Status, "processing").Set(e => e.ClaimId, record.ClaimId)
            .Set(e => e.LeaseUntil, record.LeaseUntil).Inc(e => e.Attempts, 1));
        return result.ModifiedCount == 1 ? StripeClaim.Acquired : StripeClaim.Busy;
    }
    public async Task CompleteAsync(string eventId, string claimId) {
        var result = await collection.UpdateOneAsync(e => e.EventId == eventId && e.ClaimId == claimId,
            Builders<StripeEventRecord>.Update.Set(e => e.Status, "completed").Set(e => e.ProcessedAt, DateTime.UtcNow));
        if (result.MatchedCount != 1) throw new InvalidOperationException("Webhook claim lost");
    }
    public async Task FailAsync(string eventId, string claimId) =>
        await collection.UpdateOneAsync(e => e.EventId == eventId && e.ClaimId == claimId && e.Status != "completed",
            Builders<StripeEventRecord>.Update.Set(e => e.Status, "failed"));
}
