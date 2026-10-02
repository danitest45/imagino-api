using Imagino.Api.Errors;
using Imagino.Api.Models;
using Imagino.Api.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Imagino.Api.Services.Generation;

// Job and wallet changes share a replica-set transaction. No snapshot replacement of Users.
public sealed class MongoGenerationRepository : IGenerationRepository
{
    private readonly IMongoClient client;
    private readonly IMongoCollection<GenerationJob> jobs;
    private readonly IMongoCollection<GenerationModel> models;
    private readonly IMongoCollection<User> users;
    public MongoGenerationRepository(IMongoClient client, IOptions<ImageGeneratorSettings> options)
    {
        this.client = client;
        var db = client.GetDatabase(options.Value.MongoDatabase);
        jobs = db.GetCollection<GenerationJob>("generation_jobs_v2");
        models = db.GetCollection<GenerationModel>("generation_catalog_v2");
        users = db.GetCollection<User>("Users");
    }
    public async Task InitializeAsync(IEnumerable<GenerationModel> seed, CancellationToken ct)
    {
        await jobs.Indexes.CreateManyAsync(new[] {
            new CreateIndexModel<GenerationJob>(Builders<GenerationJob>.IndexKeys.Ascending(j => j.UserId).Ascending(j => j.IdempotencyKey), new CreateIndexOptions { Unique = true, Name = "owner_idempotency" }),
            new CreateIndexModel<GenerationJob>(Builders<GenerationJob>.IndexKeys.Ascending(j => j.Status).Ascending(j => j.NextPollAt).Ascending(j => j.LeaseUntil), new CreateIndexOptions { Name = "worker_due" }),
            new CreateIndexModel<GenerationJob>(Builders<GenerationJob>.IndexKeys.Ascending(j => j.UserId).Descending(j => j.CreatedAt), new CreateIndexOptions { Name = "owner_history" })
        }, ct);
        foreach (var model in seed)
        {
            var document = model.ToBsonDocument();
            document.Remove("_id");
            await models.UpdateOneAsync(m => m.Id == model.Id, new BsonDocument("$setOnInsert", document),
                new UpdateOptions { IsUpsert = true }, ct);
        }
    }
    public Task<List<GenerationModel>> CatalogAsync(CancellationToken ct) => models.Find(_ => true).SortBy(m => m.SortOrder).ToListAsync(ct);
    public Task<GenerationJob?> FindByKeyAsync(string userId, string key, CancellationToken ct) =>
        jobs.Find(j => j.UserId == userId && j.IdempotencyKey == key).FirstOrDefaultAsync(ct)!;
    public Task<GenerationJob?> GetAsync(string id, string userId, CancellationToken ct) => !ObjectId.TryParse(id, out _) ? Task.FromResult<GenerationJob?>(null) :
        jobs.Find(j => j.Id == id && j.UserId == userId).FirstOrDefaultAsync(ct)!;
    public Task<List<GenerationJob>> HistoryAsync(string userId, CancellationToken ct) =>
        jobs.Find(j => j.UserId == userId).SortByDescending(j => j.CreatedAt).Limit(30).ToListAsync(ct);

    public async Task<GenerationJob> ReserveAsync(GenerationJob job, CancellationToken ct)
    {
        using var session = await client.StartSessionAsync(cancellationToken: ct);
        try
        {
            return await session.WithTransactionAsync(async (s, token) =>
            {
                var existing = await jobs.Find(s, j => j.UserId == job.UserId && j.IdempotencyKey == job.IdempotencyKey).FirstOrDefaultAsync(token);
                if (existing != null) return Match(existing, job);
                var debit = await users.UpdateOneAsync(s, u => u.Id == job.UserId && u.Credits >= job.Quote.Credits,
                    Builders<User>.Update.Inc(u => u.Credits, -job.Quote.Credits), cancellationToken: token);
                if (debit.ModifiedCount != 1)
                {
                    var user = await users.Find(s, u => u.Id == job.UserId).FirstOrDefaultAsync(token);
                    throw new InsufficientCreditsException(user?.Credits ?? 0, job.Quote.Credits);
                }
                await jobs.InsertOneAsync(s, job, cancellationToken: token);
                return job;
            }, cancellationToken: ct);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            var existing = await FindByKeyAsync(job.UserId, job.IdempotencyKey, ct);
            if (existing == null) throw;
            return Match(existing, job);
        }
    }
    private static GenerationJob Match(GenerationJob existing, GenerationJob requested) =>
        existing.RequestHash == requested.RequestHash ? existing : throw new ConflictAppException("Idempotency key already belongs to another request.");

    public async Task<GenerationJob?> ClaimAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var f = Builders<GenerationJob>.Filter;
        var available = f.Eq(j => j.Lease, null) | f.Lte(j => j.LeaseUntil, now);
        var due = available & (f.Eq(j => j.Status, GenerationStatus.Queued) |
            (f.Eq(j => j.Status, GenerationStatus.Processing) & f.Lte(j => j.NextPollAt, now)) |
            (f.Eq(j => j.Status, GenerationStatus.Starting) & f.Lte(j => j.DeadlineAt, now)));
        var job = await jobs.FindOneAndUpdateAsync(due,
            Builders<GenerationJob>.Update.Set(j => j.Lease, Guid.NewGuid().ToString("N")).Set(j => j.LeaseUntil, now.AddMinutes(5)),
            new FindOneAndUpdateOptions<GenerationJob> { ReturnDocument = ReturnDocument.After, Sort = Builders<GenerationJob>.Sort.Ascending(j => j.NextPollAt) }, ct);
        if (job?.Status == GenerationStatus.Queued)
        {
            var changed = await jobs.UpdateOneAsync(j => j.Id == job.Id && j.Lease == job.Lease && j.Status == GenerationStatus.Queued,
                Builders<GenerationJob>.Update.Set(j => j.Status, GenerationStatus.Starting).Set(j => j.UpdatedAt, now), cancellationToken: ct);
            if (changed.ModifiedCount != 1) return null;
            job.Status = GenerationStatus.Starting;
        }
        return job;
    }
    public async Task BindAsync(GenerationJob job, ProviderResult result, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(result.JobId)) throw new InvalidOperationException("Missing provider job binding.");
        var r = await jobs.UpdateOneAsync(j => j.Id == job.Id && j.Lease == job.Lease && j.Status == GenerationStatus.Starting,
            Builders<GenerationJob>.Update.Set(j => j.ProviderJobId, result.JobId).Set(j => j.PollingUrl, result.PollingUrl)
                .Set(j => j.ProviderReportedCostUsd, result.CostUsd).Set(j => j.Status, GenerationStatus.Processing)
                .Set(j => j.NextPollAt, DateTime.UtcNow.AddSeconds(3)).Set(j => j.UpdatedAt, DateTime.UtcNow)
                .Set(j => j.Lease, null).Set(j => j.LeaseUntil, null), cancellationToken: ct);
        if (r.ModifiedCount != 1) throw new InvalidOperationException("Provider binding lease lost.");
    }
    public async Task DeferAsync(GenerationJob job, bool failedPoll, CancellationToken ct)
    {
        var failures = failedPoll ? Math.Min(job.PollFailures + 1, 8) : 0;
        await jobs.UpdateOneAsync(j => j.Id == job.Id && j.Lease == job.Lease && j.Status == GenerationStatus.Processing,
            Builders<GenerationJob>.Update.Set(j => j.NextPollAt, DateTime.UtcNow.AddSeconds(failedPoll ? Math.Min(120, 5 * Math.Pow(2, failures)) : 5))
                .Set(j => j.PollFailures, failures).Set(j => j.Lease, null).Set(j => j.LeaseUntil, null), cancellationToken: ct);
    }
    public Task<bool> SettleAsync(GenerationJob job, GenerationStatus status, string? url, string? error, CancellationToken ct) =>
        SettleTransactionAsync(job.Id, job.UserId, job.Lease, false, status, url, error, ct);
    public Task<bool> CancelAsync(string id, string userId, CancellationToken ct) =>
        !ObjectId.TryParse(id, out _) ? Task.FromResult(false) : SettleTransactionAsync(id, userId, null, true, GenerationStatus.Cancelled, null, "cancelled", ct);

    private async Task<bool> SettleTransactionAsync(string id, string userId, string? lease, bool cancel,
        GenerationStatus status, string? url, string? error, CancellationToken ct)
    {
        if (!GenerationPolicy.IsTerminal(status)) throw new ArgumentException("Settlement requires a terminal status.");
        using var session = await client.StartSessionAsync(cancellationToken: ct);
        return await session.WithTransactionAsync(async (s, token) =>
        {
            var f = Builders<GenerationJob>.Filter;
            var filter = f.Eq(j => j.Id, id) & f.Eq(j => j.UserId, userId) & f.Eq(j => j.CreditState, CreditState.Reserved) &
                (cancel ? f.Eq(j => j.Status, GenerationStatus.Queued) : f.Eq(j => j.Lease, lease) &
                    (f.Eq(j => j.Status, GenerationStatus.Starting) | f.Eq(j => j.Status, GenerationStatus.Processing)));
            var job = await jobs.Find(s, filter).FirstOrDefaultAsync(token);
            if (job == null) return false;
            var charged = status == GenerationStatus.Completed;
            if (charged && string.IsNullOrWhiteSpace(url)) throw new ArgumentException("Completed jobs require stored output.");
            var update = Builders<GenerationJob>.Update.Set(j => j.Status, status)
                .Set(j => j.CreditState, charged ? CreditState.Charged : CreditState.Refunded)
                .Set(j => j.OutputUrl, url).Set(j => j.ErrorCode, error).Set(j => j.UpdatedAt, DateTime.UtcNow)
                .Set(j => j.Lease, null).Set(j => j.LeaseUntil, null).Set(j => j.Inputs, new List<GenerationInput>());
            var changed = await jobs.UpdateOneAsync(s, filter, update, cancellationToken: token);
            if (changed.ModifiedCount != 1) return false;
            if (!charged)
            {
                var refund = await users.UpdateOneAsync(s, u => u.Id == job.UserId,
                    Builders<User>.Update.Inc(u => u.Credits, job.Quote.Credits), cancellationToken: token);
                if (refund.MatchedCount != 1) throw new InvalidOperationException("Refund wallet not found.");
            }
            return true;
        }, cancellationToken: ct);
    }
}
