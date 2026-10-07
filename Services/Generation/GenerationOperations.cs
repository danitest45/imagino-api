using Imagino.Api.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Imagino.Api.Services.Generation;

public sealed class GenerationWorkerState
{
    private long lastTick;
    public bool Initialized { get; set; }
    public void Tick() => Interlocked.Exchange(ref lastTick, DateTime.UtcNow.Ticks);
    public bool Healthy => Initialized && DateTime.UtcNow.Ticks - Interlocked.Read(ref lastTick) < TimeSpan.FromMinutes(4).Ticks;
}
public sealed class GenerationOperations(IMongoClient client, IOptions<ImageGeneratorSettings> options,
    IGenerationOutputStore storage, GenerationProcessor processor, GenerationCostGuard? costGuard = null)
{
    private void RequireStaging()
    {
        if (options.Value.MongoDatabase != "imagino_staging") throw new Imagino.Api.Errors.ForbiddenFeatureException("Operations are restricted to staging.");
    }
    private IMongoCollection<GenerationJob> Jobs => client.GetDatabase(options.Value.MongoDatabase).GetCollection<GenerationJob>("generation_jobs_v2");
    public async Task<object> InspectAsync(CancellationToken ct)
    {
        RequireStaging();
        var now = DateTime.UtcNow;
        var stuck = await Jobs.Find(j => j.CreditState == CreditState.Reserved || j.ErrorCode == "submission_unknown")
            .SortBy(j => j.CreatedAt).Limit(100).ToListAsync(ct);
        var configuration = costGuard?.Current ?? new GenerationCostSettings();
        var day = now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var month = now.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
        var keys = configuration.Providers.Keys.Take(50).SelectMany(p => new[] { $"provider/{p}/day/{day}", $"provider/{p}/month/{month}" }).ToArray();
        var counters = await client.GetDatabase(options.Value.MongoDatabase).GetCollection<GenerationBudgetCounter>("generation_budget_counters_v1")
            .Find(Builders<GenerationBudgetCounter>.Filter.In(c => c.Id, keys)).Limit(100).ToListAsync(ct);
        var backlog = await Jobs.CountDocumentsAsync(j => j.Status == GenerationStatus.Queued || j.Status == GenerationStatus.Starting || j.Status == GenerationStatus.Processing, cancellationToken: ct);
        return new { limit = 100, emergencyStop = configuration.EmergencyStop, backlog,
            providers = configuration.Providers.Take(50).Select(p => new { provider = p.Key,
                dailyLimitUsd = p.Value.DailyUsd, monthlyLimitUsd = p.Value.MonthlyUsd,
                dailyEncumberedUsd = counters.FirstOrDefault(c => c.Id == $"provider/{p.Key}/day/{day}")?.Used ?? 0,
                monthlyEncumberedUsd = counters.FirstOrDefault(c => c.Id == $"provider/{p.Key}/month/{month}")?.Used ?? 0 }),
            jobs = stuck.Select(j => new { j.Id, status = j.Status.ToString(),
            creditState = j.CreditState.ToString(), j.ErrorCode, j.DeadlineAt, j.LeaseUntil,
            reason = j.StoredOutput != null && j.CreditState == CreditState.Reserved ? "output_stored_settlement_missing" :
                j.ErrorCode == "submission_unknown" ? "operator_provider_reconciliation_required" :
                j.Status == GenerationStatus.Starting && j.ProviderJobId == null ? "starting_without_binding" :
                j.DeadlineAt < now ? "reservation_past_deadline" : j.LeaseUntil < now ? "expired_lease" : "in_flight" }) };
    }
    public async Task<bool> ReconcileAsync(string id, CancellationToken ct)
    {
        RequireStaging();
        if (!MongoDB.Bson.ObjectId.TryParse(id, out _)) return false;
        var now = DateTime.UtcNow;
        var f = Builders<GenerationJob>.Filter;
        var filter = f.Eq(j => j.Id, id) & f.Eq(j => j.CreditState, CreditState.Reserved) &
            (f.Eq(j => j.Lease, null) | f.Lte(j => j.LeaseUntil, now)) &
            (f.Ne(j => j.StoredOutput, null) | (f.Eq(j => j.Status, GenerationStatus.Processing) & f.Ne(j => j.ProviderJobId, null)) |
                (f.Eq(j => j.Status, GenerationStatus.Starting) & f.Lte(j => j.DeadlineAt, now)));
        var job = await Jobs.FindOneAndUpdateAsync(filter, Builders<GenerationJob>.Update
            .Set(j => j.Lease, Guid.NewGuid().ToString("N")).Set(j => j.LeaseUntil, now.AddMinutes(5))
            .Push(j => j.Journal, new GenerationJournalEntry("OperatorReconciliationClaim", now)),
            new FindOneAndUpdateOptions<GenerationJob> { ReturnDocument = ReturnDocument.After }, ct);
        if (job == null) return false;
        // Reconciliation never claims queued jobs or submits paid creation POSTs.
        await processor.ProcessAsync(job, ct);
        return true;
    }
    public async Task<bool> MigrateImageAsync(string id, CancellationToken ct)
    {
        RequireStaging();
        if (!MongoDB.Bson.ObjectId.TryParse(id, out _)) return false;
        var job = await Jobs.Find(j => j.Id == id && j.Status == GenerationStatus.Completed && j.Model.MediaType == "image").FirstOrDefaultAsync(ct);
        if (job == null) return false;
        if (job.StoredOutput != null) return true;
        var original = job.OutputUrl;
        var output = await storage.DownloadAsync(job, ct);
        var url = await storage.StoreAsync(job, output.Bytes, ct);
        var changed = await Jobs.UpdateOneAsync(j => j.Id == id && j.Status == GenerationStatus.Completed && j.OutputUrl == original && j.StoredOutput == null,
            Builders<GenerationJob>.Update.Set(j => j.StoredOutput, job.StoredOutput).Set(j => j.OutputUrl, url)
                .Set(j => j.OutputStoredAtUtc, job.OutputStoredAtUtc)
                .Push(j => j.Journal, new GenerationJournalEntry("PrivateImageMigration", DateTime.UtcNow)), cancellationToken: ct);
        // Original documents/objects are retained. Public access removal is a separate verified operation.
        return changed.ModifiedCount == 1;
    }
}
