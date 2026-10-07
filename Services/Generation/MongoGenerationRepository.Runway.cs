using Imagino.Api.Errors;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Imagino.Api.Services.Generation;

public sealed partial class MongoGenerationRepository
{
    private readonly IMongoCollection<RunwaySmokeLedger> runwayLedger;
    private readonly bool runwayIntegration;
    private readonly bool runwaySmoke;
    public Task<RunwaySmokeLedger?> RunwayLedgerAsync(CancellationToken ct) =>
        runwayLedger.Find(l => l.Id == RunwaySmokePolicy.RunId).FirstOrDefaultAsync(ct)!;
    public Task<RunwaySmokeLedger?> RunwayE2eLedgerAsync(CancellationToken ct) =>
        runwayLedger.Find(l => l.Id == RunwaySmokePolicy.E2eRunId).FirstOrDefaultAsync(ct)!;
    private async Task ReserveRunwayAsync(IClientSessionHandle s, GenerationJob job, CancellationToken ct)
    {
        if (!runwaySmoke) throw new ForbiddenFeatureException("Runway authorization is closed.");
        RunwaySmokePolicy.ValidateJob(job);
        var ledger = await runwayLedger.Find(s, l => l.Id == RunwaySmokePolicy.E2eRunId).FirstOrDefaultAsync(ct);
        if (ledger == null || !RunwaySmokePolicy.CanReserve(ledger, DateTime.UtcNow))
            throw new ForbiddenFeatureException("The only Runway slot is unavailable.");
        var changed = await runwayLedger.UpdateOneAsync(s, l => l.Id == RunwaySmokePolicy.E2eRunId && !l.Halted &&
            l.State == "Available" && l.JobId == null && l.AttemptCount == 0 && l.SettlementCount == 0,
            Builders<RunwaySmokeLedger>.Update.Set(l => l.State, "Reserved").Set(l => l.JobId, job.Id), cancellationToken: ct);
        if (changed.ModifiedCount != 1) throw new ConflictAppException("Runway reservation changed.");
    }
    public async Task<bool> BeginRunwaySubmissionAsync(GenerationJob job, CancellationToken ct)
    {
        if (!runwaySmoke) return false;
        RunwaySmokePolicy.ValidateJob(job);
        using var session = await client.StartSessionAsync(cancellationToken: ct);
        return await session.WithTransactionAsync(async (s, token) => {
            var now = DateTime.UtcNow;
            var changed = await runwayLedger.UpdateOneAsync(s, l => l.Id == RunwaySmokePolicy.E2eRunId &&
                l.OwnerId == job.UserId && l.SourceAssetId == job.SourceAssetId && l.BudgetUsd == RunwaySmokePolicy.CeilingUsd &&
                !l.Halted && l.ExpiresAtUtc > now && l.State == "Reserved" && l.JobId == job.Id && l.AttemptCount == 0 && l.TaskId == null,
                Builders<RunwaySmokeLedger>.Update.Set(l => l.State, "SubmissionAttempted").Set(l => l.AttemptedAtUtc, now)
                    .Inc(l => l.AttemptCount, 1), cancellationToken: token);
            if (changed.ModifiedCount != 1) return false;
            var marked = await jobs.UpdateOneAsync(s, j => j.Id == job.Id && j.Lease == job.Lease &&
                j.Status == GenerationStatus.Starting && j.ProviderJobId == null && j.CreditState == CreditState.Reserved,
                Builders<GenerationJob>.Update.Push(j => j.Journal, new GenerationJournalEntry("RunwayPostAttemptAuthorized", now)), cancellationToken: token);
            if (marked.ModifiedCount != 1) throw new ConflictAppException("Runway lease lost before POST.");
            return true;
        }, cancellationToken: ct);
    }
    private async Task BindRunwayAsync(GenerationJob job, ProviderResult result, FilterDefinition<GenerationJob> filter,
        UpdateDefinition<GenerationJob> update, CancellationToken ct)
    {
        RunwayGenerationProvider.ValidateTaskId(result.JobId!);
        if (result.PollingUrl != RunwayGenerationProvider.Origin + "/v1/tasks/" + result.JobId)
            throw new InvalidDataException("Invalid Runway task binding.");
        using var session = await client.StartSessionAsync(cancellationToken: ct);
        await session.WithTransactionAsync(async (s, token) => {
            // A lost commit acknowledgement may leave the binding already durable.
            var existing = await jobs.Find(s, j => j.Id == job.Id && j.UserId == job.UserId &&
                j.ProviderJobId == result.JobId && j.PollingUrl == result.PollingUrl &&
                j.Status == GenerationStatus.Processing).FirstOrDefaultAsync(token);
            if (existing != null)
            {
                var accepted = await runwayLedger.Find(s, l => l.Id == RunwaySmokePolicy.E2eRunId &&
                    l.JobId == job.Id && l.TaskId == result.JobId && l.AttemptCount == 1 && l.State == "Accepted").FirstOrDefaultAsync(token);
                if (accepted == null) throw new InvalidOperationException("Runway durable binding is inconsistent.");
                return true;
            }
            var r = await jobs.UpdateOneAsync(s, filter & Builders<GenerationJob>.Filter.Eq(j => j.ProviderJobId, null),
                update.Set(j => j.ProviderStatus, "PENDING").Set(j => j.ProviderHttpStatus, result.HttpStatus), cancellationToken: token);
            if (r.ModifiedCount != 1) throw new InvalidOperationException("Runway binding lease lost.");
            var u = Builders<RunwaySmokeLedger>.Update.Set(l => l.State, "Accepted").Set(l => l.TaskId, result.JobId)
                .Set(l => l.AcceptedAtUtc, DateTime.UtcNow);
            if (result.EstimatedCostUsd != RunwaySmokePolicy.CostUsd)
                u = u.Set(l => l.Halted, true).Set(l => l.HaltReason, "provider_estimate_changed");
            var b = await runwayLedger.UpdateOneAsync(s, l => l.Id == RunwaySmokePolicy.E2eRunId && l.JobId == job.Id &&
                l.State == "SubmissionAttempted" && l.AttemptCount == 1 && l.TaskId == null, u, cancellationToken: token);
            if (b.ModifiedCount != 1) throw new InvalidOperationException("Runway task ledger lost.");
            return true;
        }, cancellationToken: ct);
    }
    public async Task RecordRunwayPollAsync(GenerationJob job, ProviderResult result, CancellationToken ct)
    {
        if (job.ProviderJobId == null || result.JobId != job.ProviderJobId) throw new InvalidDataException("Runway poll binding mismatch.");
        var now = DateTime.UtcNow;
        var running = job.ProviderRunningAtUtc ?? (result.ProviderStatus == "RUNNING" ? now : (DateTime?)null);
        var ready = job.ProviderReadyAtUtc ?? (result.ProviderStatus == "SUCCEEDED" ? now : (DateTime?)null);
        using var session = await client.StartSessionAsync(cancellationToken: ct);
        await session.WithTransactionAsync(async (s, token) => {
            var u = Builders<GenerationJob>.Update.Set(j => j.ProviderStatus, result.ProviderStatus)
                .Set(j => j.ProviderRunningAtUtc, running).Set(j => j.ProviderReadyAtUtc, ready).Inc(j => j.ProviderPollCount, 1);
            if (result.CostUsd != null) u = u.Set(j => j.ProviderReportedCostUsd, result.CostUsd);
            if (job.ProviderStatus != result.ProviderStatus)
                u = u.Push(j => j.Journal, new GenerationJournalEntry("RunwayState_" + result.ProviderStatus, now));
            var r = await jobs.UpdateOneAsync(s, j => j.Id == job.Id && j.Lease == job.Lease &&
                j.Status == GenerationStatus.Processing && j.ProviderJobId == result.JobId, u, cancellationToken: token);
            if (r.ModifiedCount != 1) throw new InvalidOperationException("Runway poll lease lost.");
            var l = Builders<RunwaySmokeLedger>.Update.Set(x => x.ObservedUsd, result.CostUsd);
            if (result.EstimatedCostUsd > RunwaySmokePolicy.CeilingUsd || result.CostUsd > RunwaySmokePolicy.CeilingUsd)
                l = l.Set(x => x.Halted, true).Set(x => x.HaltReason, "cost_ceiling_exceeded");
            if (result.CostUsd != null || result.EstimatedCostUsd > RunwaySmokePolicy.CeilingUsd)
            {
                var changed = await runwayLedger.UpdateOneAsync(s, x => x.Id == RunwaySmokePolicy.E2eRunId &&
                    x.JobId == job.Id && x.TaskId == result.JobId && x.AttemptCount == 1, l, cancellationToken: token);
                if (changed.MatchedCount != 1) throw new InvalidOperationException("Runway poll ledger binding lost.");
            }
            return true;
        }, cancellationToken: ct);
        job.ProviderStatus = result.ProviderStatus; job.ProviderRunningAtUtc = running; job.ProviderReadyAtUtc = ready;
        if (result.CostUsd != null) job.ProviderReportedCostUsd = result.CostUsd;
    }
}
