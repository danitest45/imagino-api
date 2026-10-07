using Imagino.Api.Errors;
using Imagino.Api.Models;
using Imagino.Api.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Imagino.Api.Services.Generation;

// Job and wallet changes share a replica-set transaction. No snapshot replacement of Users.
public sealed partial class MongoGenerationRepository : IGenerationRepository
{
    private readonly IMongoClient client;
    private readonly IMongoCollection<GenerationJob> jobs;
    private readonly IMongoCollection<GenerationModel> models;
    private readonly IMongoCollection<User> users;
    private readonly IMongoCollection<BflHomologationLedger> bflLedger;
    private readonly bool homologation;
    private readonly IMongoCollection<OpenAiHomologationLedger> openAiLedger;
    private readonly bool openAiHomologation;
    private readonly bool singleSmoke;
    public MongoGenerationRepository(IMongoClient client, IOptions<ImageGeneratorSettings> options, IOptions<GenerationSettings> generation)
    {
        this.client = client;
        var db = client.GetDatabase(options.Value.MongoDatabase);
        jobs = db.GetCollection<GenerationJob>("generation_jobs_v2");
        models = db.GetCollection<GenerationModel>("generation_catalog_v2");
        users = db.GetCollection<User>("Users");
        bflLedger = db.GetCollection<BflHomologationLedger>("generation_bfl_homologation_v1");
        homologation = generation.Value.BflHomologationEnabled;
        openAiLedger = db.GetCollection<OpenAiHomologationLedger>("generation_openai_homologation_v1");
        openAiHomologation = generation.Value.OpenAiHomologationEnabled;
        singleSmoke = generation.Value.OpenAiSingleSmokeEnabled && generation.Value.PaidGenerationEnabled;
        runwayLedger = db.GetCollection<RunwaySmokeLedger>("generation_runway_single_video_v1");
        runwayIntegration = generation.Value.RunwayIntegrationEnabled;
        runwaySmoke = runwayIntegration && generation.Value.RunwayRealSmokeEnabled && generation.Value.PaidGenerationEnabled && !generation.Value.OpenAiSingleSmokeEnabled;
    }
    public async Task InitializeAsync(IEnumerable<GenerationModel> seed, CancellationToken ct)
    {
        if (runwayIntegration)
        {
            var initial = new RunwaySmokeLedger().ToBsonDocument(); initial.Remove("_id");
            // Only create the newly authorized ledger. Never update or reset the first smoke.
            await runwayLedger.UpdateOneAsync(l => l.Id == RunwaySmokePolicy.E2eRunId,
                new BsonDocument("$setOnInsert", initial), new UpdateOptions { IsUpsert = true }, ct);
            await jobs.Indexes.CreateOneAsync(new CreateIndexModel<GenerationJob>(Builders<GenerationJob>.IndexKeys.Ascending(j => j.ProviderJobId),
                new CreateIndexOptions<GenerationJob> { Unique = true, Name = "runway_task_unique", PartialFilterExpression =
                    new BsonDocument { { "Model.Provider", "runway" }, { "ProviderJobId", new BsonDocument("$type", "string") } } }), cancellationToken: ct);
        }
        if (openAiHomologation)
        {
            var initial = new OpenAiHomologationLedger().ToBsonDocument(); initial.Remove("_id");
            await openAiLedger.UpdateOneAsync(l => l.Id == OpenAiHomologationPolicy.RunId,
                new BsonDocument("$setOnInsert", initial), new UpdateOptions { IsUpsert = true }, ct);
            var smoke = OpenAiSingleSmokePolicy.NewLedger().ToBsonDocument(); smoke.Remove("_id");
            await openAiLedger.UpdateOneAsync(l => l.Id == OpenAiSingleSmokePolicy.RunId,
                new BsonDocument("$setOnInsert", smoke), new UpdateOptions { IsUpsert = true }, ct);
        }
        if (homologation)
        {
            var ledger = new BflHomologationLedger().ToBsonDocument(); ledger.Remove("_id");
            await bflLedger.UpdateOneAsync(l => l.Id == BflHomologationPolicy.RunId,
                new BsonDocument("$setOnInsert", ledger), new UpdateOptions { IsUpsert = true }, ct);
        }
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
            // Narrow, replay-safe migration of our two original staging seed rows.
            // Keep pricing, capabilities and any independently managed catalog entries.
            if (model.Provider == "google-veo" && model.Version == "2026-10-02.2")
                await models.UpdateOneAsync(m => m.Id == model.Id && m.Version == "2026-10-02.1" && m.ProviderModel == model.ProviderModel,
                    Builders<GenerationModel>.Update.Set(m => m.Version, model.Version)
                        .Set(m => m.Lifecycle, model.Lifecycle).Set(m => m.Description, model.Description), cancellationToken: ct);
        }
    }
    public Task<List<GenerationModel>> CatalogAsync(CancellationToken ct) => models.Find(_ => true).SortBy(m => m.SortOrder).ToListAsync(ct);
    public Task<OpenAiHomologationLedger?> SingleSmokeLedgerAsync(CancellationToken ct) =>
        openAiLedger.Find(l => l.Id == OpenAiSingleSmokePolicy.RunId).FirstOrDefaultAsync(ct)!;
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
                if (job.Model.Provider == "runway") await ReserveRunwayAsync(s, job, token);
                if (job.Model.Provider == "openai")
                {
                    if (job.OpenAiRunId == OpenAiSingleSmokePolicy.RunId)
                    {
                        if (!openAiHomologation || !singleSmoke) throw new ForbiddenFeatureException("Single Flare authorization is closed.");
                        OpenAiSingleSmokePolicy.ValidateJob(job);
                        job.OpenAiHomologationCall = 1;
                        var ledger = await openAiLedger.Find(s, l => l.Id == OpenAiSingleSmokePolicy.RunId).FirstOrDefaultAsync(token);
                        if (ledger == null || !OpenAiSingleSmokePolicy.CanReserve(ledger, DateTime.UtcNow))
                            throw new ForbiddenFeatureException("The only Flare slot is unavailable.");
                        var f = Builders<OpenAiHomologationLedger>.Filter;
                        var changed = await openAiLedger.UpdateOneAsync(s, f.Eq(l => l.Id, ledger.Id) & f.Eq(l => l.CommittedUsd, 0) &
                            f.Eq(l => l.Halted, false) & f.Eq("Calls.1.State", "Available") & f.Eq("Calls.1.JobId", BsonNull.Value),
                            Builders<OpenAiHomologationLedger>.Update.Set("Calls.1.State", "Reserved").Set("Calls.1.JobId", job.Id)
                                .Inc(l => l.CommittedUsd, OpenAiSingleSmokePolicy.ProjectionUsd), cancellationToken: token);
                        if (changed.ModifiedCount != 1) throw new ConflictAppException("Single Flare reservation changed.");
                    }
                    else
                    {
                    if (!openAiHomologation || !OpenAiHomologationPolicy.CostBoundsVerified) throw new ForbiddenFeatureException("OpenAI financial gate is closed.");
                    var call = OpenAiHomologationPolicy.ValidateJob(job);
                    var maximum = OpenAiHomologationPolicy.VerifiedMaximumUsd(call) ?? throw new ForbiddenFeatureException("Unknown maximum OpenAI cost.");
                    job.OpenAiHomologationCall = call;
                    var ledger = await openAiLedger.Find(s, l => l.Id == OpenAiHomologationPolicy.RunId).FirstOrDefaultAsync(token);
                    if (ledger == null || !OpenAiHomologationPolicy.CanReserve(ledger, call, maximum, DateTime.UtcNow))
                        throw new ForbiddenFeatureException("OpenAI slot is consumed, unreconciled, halted or outside budget.");
                    var f = Builders<OpenAiHomologationLedger>.Filter;
                    var changed = await openAiLedger.UpdateOneAsync(s, f.Eq(l => l.Id, ledger.Id) & f.Eq(l => l.CommittedUsd, ledger.CommittedUsd) &
                        f.Eq(l => l.Halted, false) & f.Eq($"Calls.{call}.State", "Available"), Builders<OpenAiHomologationLedger>.Update
                        .Set($"Calls.{call}.State", "Reserved").Set($"Calls.{call}.JobId", job.Id).Set($"Calls.{call}.MaximumUsd", maximum)
                        .Inc(l => l.CommittedUsd, maximum), cancellationToken: token);
                    if (changed.ModifiedCount != 1) throw new ConflictAppException("OpenAI reservation ledger changed.");
                    }
                }
                if (homologation && job.Model.Provider == "bfl")
                {
                    var call = BflHomologationPolicy.ValidateJob(job);
                    job.BflHomologationCall = call;
                    var f = Builders<BflHomologationLedger>.Filter;
                    var allowed = f.Eq(l => l.Id, BflHomologationPolicy.RunId) & f.Eq(l => l.OwnerId, job.UserId) &
                        f.Eq(l => l.Halted, false) & f.Gt(l => l.ExpiresAtUtc, DateTime.UtcNow) &
                        f.Eq(l => l.BudgetUsd, BflHomologationPolicy.BudgetUsd) &
                        f.Lte(l => l.CommittedUsd, BflHomologationPolicy.BudgetUsd - BflHomologationPolicy.Cost(call)) &
                        f.Eq($"Calls.{call}.State", "Available");
                    if (call > 1) allowed &= f.Eq($"Calls.{call - 1}.State", "Completed") & f.Eq($"Calls.{call - 1}.Reconciled", true);
                    var slot = await bflLedger.UpdateOneAsync(s, allowed, Builders<BflHomologationLedger>.Update
                        .Set($"Calls.{call}.State", "Reserved").Set($"Calls.{call}.JobId", job.Id)
                        .Inc(l => l.CommittedUsd, BflHomologationPolicy.Cost(call)), cancellationToken: token);
                    if (slot.ModifiedCount != 1) throw new ForbiddenFeatureException("BFL call is consumed, unreconciled, halted or outside its budget.");
                }
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

    public async Task<bool> BeginBflSubmissionAsync(GenerationJob job, CancellationToken ct)
    {
        if (!homologation) return false;
        var call = BflHomologationPolicy.ValidateJob(job);
        using var session = await client.StartSessionAsync(cancellationToken: ct);
        return await session.WithTransactionAsync(async (s, token) =>
        {
            var f = Builders<BflHomologationLedger>.Filter;
            var allowed = f.Eq(l => l.Id, BflHomologationPolicy.RunId) & f.Eq(l => l.OwnerId, job.UserId) & f.Eq(l => l.Halted, false) &
                f.Eq($"Calls.{call}.JobId", job.Id) & f.Eq($"Calls.{call}.State", "Reserved");
            var now = DateTime.UtcNow;
            var changed = await bflLedger.UpdateOneAsync(s, allowed, Builders<BflHomologationLedger>.Update
                .Set($"Calls.{call}.State", "SubmissionAttempted").Set($"Calls.{call}.AttemptedAtUtc", now), cancellationToken: token);
            if (changed.ModifiedCount != 1) return false;
            var marked = await jobs.UpdateOneAsync(s, j => j.Id == job.Id && j.Lease == job.Lease && j.Status == GenerationStatus.Starting && j.ProviderJobId == null,
                Builders<GenerationJob>.Update.Push(j => j.Journal, new GenerationJournalEntry("BflPostAttemptAuthorized", now)), cancellationToken: token);
            if (marked.ModifiedCount != 1) throw new ConflictAppException("BFL submission lease lost before POST.");
            return true;
        }, cancellationToken: ct);
    }

    public async Task<bool> BeginOpenAiSubmissionAsync(GenerationJob job, CancellationToken ct)
    {
        if (job.OpenAiRunId == OpenAiSingleSmokePolicy.RunId)
        {
            if (!openAiHomologation || !singleSmoke) return false;
            OpenAiSingleSmokePolicy.ValidateJob(job);
            using var smokeSession = await client.StartSessionAsync(cancellationToken: ct);
            return await smokeSession.WithTransactionAsync(async (s, token) =>
            {
                var f = Builders<OpenAiHomologationLedger>.Filter;
                var now = DateTime.UtcNow;
                var changed = await openAiLedger.UpdateOneAsync(s, f.Eq(l => l.Id, OpenAiSingleSmokePolicy.RunId) &
                    f.Eq(l => l.OwnerId, job.UserId) & f.Eq(l => l.Halted, false) & f.Gt(l => l.ExpiresAtUtc, now) &
                    f.Eq(l => l.BudgetUsd, OpenAiSingleSmokePolicy.ObservedCeilingUsd) &
                    f.Eq("Calls.1.JobId", job.Id) & f.Eq("Calls.1.State", "Reserved"),
                    Builders<OpenAiHomologationLedger>.Update.Set("Calls.1.State", "SubmissionAttempted")
                        .Set("Calls.1.AttemptedAtUtc", now), cancellationToken: token);
                if (changed.ModifiedCount != 1) return false;
                var marked = await jobs.UpdateOneAsync(s, j => j.Id == job.Id && j.Lease == job.Lease && j.Status == GenerationStatus.Starting,
                    Builders<GenerationJob>.Update.Push(j => j.Journal, new GenerationJournalEntry("OpenAiPostAttemptAuthorized", now)), cancellationToken: token);
                if (marked.ModifiedCount != 1) throw new ConflictAppException("Single Flare lease lost before POST.");
                return true;
            }, cancellationToken: ct);
        }
        if (!openAiHomologation || !OpenAiHomologationPolicy.CostBoundsVerified) return false;
        var call = OpenAiHomologationPolicy.ValidateJob(job);
        using var session = await client.StartSessionAsync(cancellationToken: ct);
        return await session.WithTransactionAsync(async (s, token) =>
        {
            var f = Builders<OpenAiHomologationLedger>.Filter;
            var maximum = OpenAiHomologationPolicy.VerifiedMaximumUsd(call)!.Value;
            var allowed = f.Eq(l => l.Id, OpenAiHomologationPolicy.RunId) & f.Eq(l => l.OwnerId, job.UserId) &
                f.Eq(l => l.Halted, false) & f.Gt(l => l.ExpiresAtUtc, DateTime.UtcNow) & f.Eq(l => l.BudgetUsd, OpenAiHomologationPolicy.BudgetUsd) &
                f.Lte(l => l.CommittedUsd, OpenAiHomologationPolicy.BudgetUsd) & f.Eq($"Calls.{call}.MaximumUsd", maximum) &
                f.Eq($"Calls.{call}.JobId", job.Id) & f.Eq($"Calls.{call}.State", "Reserved");
            var changed = await openAiLedger.UpdateOneAsync(s, allowed, Builders<OpenAiHomologationLedger>.Update
                .Set($"Calls.{call}.State", "SubmissionAttempted").Set($"Calls.{call}.AttemptedAtUtc", DateTime.UtcNow), cancellationToken: token);
            if (changed.ModifiedCount != 1) return false;
            var marked = await jobs.UpdateOneAsync(s, j => j.Id == job.Id && j.Lease == job.Lease && j.Status == GenerationStatus.Starting,
                Builders<GenerationJob>.Update.Push(j => j.Journal, new GenerationJournalEntry("OpenAiPostAttemptAuthorized", DateTime.UtcNow)), cancellationToken: token);
            if (marked.ModifiedCount != 1) throw new ConflictAppException("OpenAI submission lease lost before POST.");
            return true;
        }, cancellationToken: ct);
    }

    public async Task RecordSynchronousResultAsync(GenerationJob job, ProviderResult result, CancellationToken ct)
    {
        if (!result.Completed || result.Bytes == null && result.ErrorCode == null) throw new InvalidDataException("Missing synchronous provider result.");
        if (job.Model.Provider == "openai" && (result.Usage == null || result.CostUsd != OpenAiImagePricing.Calculate(result.Usage)))
            throw new InvalidDataException("Inconsistent OpenAI measured cost.");
        using var session = await client.StartSessionAsync(cancellationToken: ct);
        await session.WithTransactionAsync(async (s, token) =>
        {
            var changed = await jobs.UpdateOneAsync(s, j => j.Id == job.Id && j.Lease == job.Lease && j.Status == GenerationStatus.Starting && j.SynchronousResponseAtUtc == null,
                Builders<GenerationJob>.Update.Set(j => j.ProviderUsage, result.Usage).Set(j => j.ProviderReportedCostUsd, result.CostUsd)
                    .Set(j => j.ProviderAcceptanceLatencyMs, result.AcceptanceLatencyMs).Set(j => j.SynchronousResponseAtUtc, DateTime.UtcNow)
                    .Set(j => j.ProviderDecodeLatencyMs, result.DecodeLatencyMs).Set(j => j.ProviderHttpStatus, result.HttpStatus)
                    .Push(j => j.Journal, new GenerationJournalEntry("SynchronousResponseReceived", DateTime.UtcNow)), cancellationToken: token);
            if (changed.ModifiedCount != 1) throw new InvalidOperationException("Synchronous result lease lost.");
            if (job.Model.Provider == "openai")
            {
                if (!openAiHomologation || result.CostUsd == null || result.Usage == null) throw new InvalidDataException("Missing OpenAI measured cost.");
                var call = job.OpenAiHomologationCall ?? throw new InvalidOperationException("Missing OpenAI slot.");
                var f = Builders<OpenAiHomologationLedger>.Filter;
                var isSmoke = job.OpenAiRunId == OpenAiSingleSmokePolicy.RunId;
                var ledger = await openAiLedger.Find(s, f.Eq(l => l.Id, isSmoke ? OpenAiSingleSmokePolicy.RunId : OpenAiHomologationPolicy.RunId) & f.Eq($"Calls.{call}.JobId", job.Id) &
                    f.Eq($"Calls.{call}.State", "SubmissionAttempted")).FirstOrDefaultAsync(token);
                if (ledger == null) throw new InvalidOperationException("OpenAI response ledger lost.");
                var reservedUsd = isSmoke ? ledger.Calls[call.ToString()].ProjectedUsd!.Value : ledger.Calls[call.ToString()].MaximumUsd;
                var actual = result.CostUsd.Value;
                var exceeded = (!isSmoke && actual > reservedUsd) || ledger.ObservedUsd + actual > ledger.BudgetUsd;
                var update = Builders<OpenAiHomologationLedger>.Update.Set($"Calls.{call}.State", "ResponseReceived")
                    .Set($"Calls.{call}.ObservedUsd", actual).Set($"Calls.{call}.Usage", result.Usage)
                    .Set($"Calls.{call}.ResponseAtUtc", DateTime.UtcNow).Inc(l => l.ObservedUsd, actual)
                    .Inc(l => l.CommittedUsd, actual - reservedUsd);
                if (exceeded) update = update.Set(l => l.Halted, true).Set(l => l.HaltReason, isSmoke ? "observed_ceiling_exceeded" : "cost_bound_exceeded");
                await openAiLedger.UpdateOneAsync(s, l => l.Id == ledger.Id, update, cancellationToken: token);
            }
            return true;
        }, cancellationToken: ct);
        job.ProviderUsage = result.Usage; job.ProviderReportedCostUsd = result.CostUsd;
        job.ProviderAcceptanceLatencyMs = result.AcceptanceLatencyMs; job.SynchronousResponseAtUtc = DateTime.UtcNow;
        job.ProviderDecodeLatencyMs = result.DecodeLatencyMs; job.ProviderHttpStatus = result.HttpStatus;
    }

    public async Task<GenerationJob?> ClaimAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var f = Builders<GenerationJob>.Filter;
        var available = f.Eq(j => j.Lease, null) | f.Lte(j => j.LeaseUntil, now);
        var due = available & (f.Eq(j => j.Status, GenerationStatus.Queued) |
            (f.Eq(j => j.Status, GenerationStatus.Processing) & f.Lte(j => j.NextPollAt, now)) |
            (f.Eq(j => j.Status, GenerationStatus.Starting) & f.Lte(j => j.DeadlineAt, now)));
        var job = await jobs.FindOneAndUpdateAsync(due,
            Builders<GenerationJob>.Update.Set(j => j.Lease, Guid.NewGuid().ToString("N")).Set(j => j.LeaseUntil, now.AddMinutes(5))
                .Push(j => j.Journal, new GenerationJournalEntry("WorkerClaim", now)),
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
        var update = Builders<GenerationJob>.Update.Set(j => j.ProviderJobId, result.JobId).Set(j => j.PollingUrl, result.PollingUrl)
                .Set(j => j.ProviderReportedCostUsd, result.CostUsd).Set(j => j.Status, GenerationStatus.Processing)
                .Set(j => j.ProviderAcceptanceLatencyMs, result.AcceptanceLatencyMs)
                .Set(j => j.NextPollAt, DateTime.UtcNow.AddSeconds(job.Model.Provider == "runway" ? 5 : 3)).Set(j => j.UpdatedAt, DateTime.UtcNow)
                .Push(j => j.Journal, new GenerationJournalEntry("ProviderBound", DateTime.UtcNow))
                .Set(j => j.Lease, null).Set(j => j.LeaseUntil, null);
        var filter = Builders<GenerationJob>.Filter.Where(j => j.Id == job.Id && j.Lease == job.Lease && j.Status == GenerationStatus.Starting);
        if (job.Model.Provider == "runway")
        {
            await BindRunwayAsync(job, result, filter, update, ct);
            return;
        }
        if (!homologation || job.Model.Provider != "bfl")
        {
            var r = await jobs.UpdateOneAsync(filter, update, cancellationToken: ct);
            if (r.ModifiedCount != 1) throw new InvalidOperationException("Provider binding lease lost.");
            return;
        }
        var call = job.BflHomologationCall ?? throw new InvalidOperationException("Missing BFL authorization.");
        using var session = await client.StartSessionAsync(cancellationToken: ct);
        await session.WithTransactionAsync(async (s, token) =>
        {
            var r = await jobs.UpdateOneAsync(s, filter, update, cancellationToken: token);
            if (r.ModifiedCount != 1) throw new InvalidOperationException("Provider binding lease lost.");
            var f = Builders<BflHomologationLedger>.Filter;
            var ledgerUpdate = Builders<BflHomologationLedger>.Update.Set($"Calls.{call}.State", "Accepted")
                .Set($"Calls.{call}.ReportedUsd", result.CostUsd).Set($"Calls.{call}.AcceptedAtUtc", DateTime.UtcNow);
            if (result.CostUsd != BflHomologationPolicy.Cost(call))
                ledgerUpdate = ledgerUpdate.Set(l => l.Halted, true).Set(l => l.HaltReason, result.CostUsd == null ? "cost_unreported" : "cost_changed");
            var bound = await bflLedger.UpdateOneAsync(s, f.Eq(l => l.Id, BflHomologationPolicy.RunId) &
                f.Eq($"Calls.{call}.JobId", job.Id) & f.Eq($"Calls.{call}.State", "SubmissionAttempted"), ledgerUpdate, cancellationToken: token);
            if (bound.ModifiedCount != 1) throw new InvalidOperationException("BFL ledger binding lost.");
            return true;
        }, cancellationToken: ct);
    }
    public async Task DeferAsync(GenerationJob job, bool failedPoll, CancellationToken ct)
    {
        var failures = failedPoll ? Math.Min(job.PollFailures + 1, 8) : 0;
        await jobs.UpdateOneAsync(j => j.Id == job.Id && j.Lease == job.Lease && j.Status == GenerationStatus.Processing,
            Builders<GenerationJob>.Update.Set(j => j.NextPollAt, DateTime.UtcNow.AddSeconds(Math.Max(5,
                Math.Max(job.PollDelaySeconds ?? 0, failedPoll ? Math.Min(120, 5 * Math.Pow(2, failures)) : 5))))
                .Set(j => j.PollFailures, failures).Set(j => j.Lease, null).Set(j => j.LeaseUntil, null), cancellationToken: ct);
    }
    public Task<bool> SettleAsync(GenerationJob job, GenerationStatus status, string? url, string? error, CancellationToken ct) =>
        SettleTransactionAsync(job.Id, job.UserId, job.Lease, false, status, url, error, job.OutputMetrics, ct);
    public Task<bool> CancelAsync(string id, string userId, CancellationToken ct) =>
        !ObjectId.TryParse(id, out _) ? Task.FromResult(false) : SettleTransactionAsync(id, userId, null, true, GenerationStatus.Cancelled, null, "cancelled", null, ct);

    private async Task<bool> SettleTransactionAsync(string id, string userId, string? lease, bool cancel,
        GenerationStatus status, string? url, string? error, GenerationOutputMetrics? metrics, CancellationToken ct)
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
                .Set(j => j.OutputMetrics, metrics)
                .Set(j => j.Lease, null).Set(j => j.LeaseUntil, null).Set(j => j.Inputs, new List<GenerationInput>());
            var events = new List<GenerationJournalEntry>();
            if (charged) events.Add(new("OutputStored", DateTime.UtcNow));
            events.Add(new(charged ? "CompletedCharged" : status == GenerationStatus.Cancelled ? "CancelledRefunded" : "FailedRefunded", DateTime.UtcNow));
            update = update.PushEach(j => j.Journal, events);
            var changed = await jobs.UpdateOneAsync(s, filter, update, cancellationToken: token);
            if (changed.ModifiedCount != 1) return false;
            if (job.Model.Provider == "runway")
            {
                var settledRunway = await runwayLedger.UpdateOneAsync(s, l => l.Id == RunwaySmokePolicy.E2eRunId && l.JobId == job.Id && l.SettlementCount == 0,
                    Builders<RunwaySmokeLedger>.Update.Set(l => l.State, status.ToString()).Set(l => l.Halted, true)
                        .Set(l => l.HaltReason, charged ? "single_video_completed" : error ?? "generation_failed")
                        .Set(l => l.SettledAtUtc, DateTime.UtcNow).Inc(l => l.SettlementCount, 1), cancellationToken: token);
                if (settledRunway.ModifiedCount != 1) throw new InvalidOperationException("Runway settlement ledger lost.");
            }
            if (!charged)
            {
                var refund = await users.UpdateOneAsync(s, u => u.Id == job.UserId,
                    Builders<User>.Update.Inc(u => u.Credits, job.Quote.Credits), cancellationToken: token);
                if (refund.MatchedCount != 1) throw new InvalidOperationException("Refund wallet not found.");
            }
            if (homologation && job.Model.Provider == "bfl")
            {
                var call = job.BflHomologationCall ?? throw new InvalidOperationException("Missing BFL authorization.");
                var fLedger = Builders<BflHomologationLedger>.Filter;
                var ledgerUpdate = Builders<BflHomologationLedger>.Update.Set($"Calls.{call}.State", charged ? "Completed" : "Failed")
                    .Set($"Calls.{call}.SettledAtUtc", DateTime.UtcNow);
                if (!charged) ledgerUpdate = ledgerUpdate.Set(l => l.Halted, true).Set(l => l.HaltReason, error ?? "generation_failed");
                var settled = await bflLedger.UpdateOneAsync(s, fLedger.Eq(l => l.Id, BflHomologationPolicy.RunId) &
                    fLedger.Eq($"Calls.{call}.JobId", job.Id), ledgerUpdate, cancellationToken: token);
                if (settled.MatchedCount != 1) throw new InvalidOperationException("BFL ledger settlement lost.");
            }
            if (job.Model.Provider == "openai")
            {
                var call = job.OpenAiHomologationCall ?? throw new InvalidOperationException("Missing OpenAI authorization.");
                var fLedger = Builders<OpenAiHomologationLedger>.Filter;
                var updateLedger = Builders<OpenAiHomologationLedger>.Update.Set($"Calls.{call}.State", charged ? "Completed" : "Failed")
                    .Set($"Calls.{call}.Reconciled", charged && job.ProviderUsage != null && job.ProviderReportedCostUsd != null);
                // Any failure/ambiguity stops the entire run. A third completion also closes it.
                var isSmoke = job.OpenAiRunId == OpenAiSingleSmokePolicy.RunId;
                if (!charged || call == 3 || isSmoke) updateLedger = updateLedger.Set(l => l.Halted, true)
                    .Set(l => l.HaltReason, charged ? isSmoke ? "single_call_completed" : "three_calls_completed" : error ?? "generation_failed");
                var settled = await openAiLedger.UpdateOneAsync(s, fLedger.Eq(l => l.Id, isSmoke ? OpenAiSingleSmokePolicy.RunId : OpenAiHomologationPolicy.RunId) &
                    fLedger.Eq($"Calls.{call}.JobId", job.Id), updateLedger, cancellationToken: token);
                if (settled.MatchedCount != 1) throw new InvalidOperationException("OpenAI ledger settlement lost.");
            }
            return true;
        }, cancellationToken: ct);
    }
}
