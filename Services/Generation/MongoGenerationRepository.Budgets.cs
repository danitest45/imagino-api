using Imagino.Api.Errors;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Imagino.Api.Services.Generation;

public sealed partial class MongoGenerationRepository
{
    private readonly IMongoCollection<GenerationBudgetCounter> budgetCounters;
    private readonly IMongoCollection<GenerationBudgetReservation> budgetReservations;
    private readonly GenerationCostGuard? costGuard;

    private async Task ReserveLaunchBudgetsAsync(IClientSessionHandle session, GenerationJob job, CancellationToken ct)
    {
        if (job.Model.Provider == "fixture") return;
        if (costGuard == null) throw new ForbiddenFeatureException("Cost controls are required.");
        var now = DateTime.UtcNow;
        var approval = costGuard.Validate(job.Model, job.Quote, now);
        var settings = costGuard.Current;
        var provider = settings.Providers[job.Model.Provider];
        var day = now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var month = now.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
        var costs = new[] { $"provider/{job.Model.Provider}/day/{day}", $"provider/{job.Model.Provider}/month/{month}", $"model/{job.Model.Id}/day/{day}" };
        var limits = new[] { provider.DailyUsd, provider.MonthlyUsd, approval.DailyUsd };
        for (var i = 0; i < costs.Length; i++) await ReserveCounterAsync(session, costs[i], approval.MaximumRequestUsd, limits[i], ct);
        await ReserveCounterAsync(session, $"user/{job.UserId}/credits/{day}", job.Quote.Credits, settings.UserDailyCredits, ct);
        var concurrency = new List<string> { $"user/{job.UserId}/active" };
        await ReserveCounterAsync(session, concurrency[0], 1, settings.UserConcurrentJobs, ct);
        if (job.Model.MediaType == "video")
        {
            concurrency.Add($"user/{job.UserId}/video-active");
            await ReserveCounterAsync(session, concurrency[1], 1, settings.UserConcurrentVideos, ct);
        }
        job.BudgetReservationId = job.Id;
        await budgetReservations.InsertOneAsync(session, new GenerationBudgetReservation {
            Id = job.Id, Owner = job.UserId, Usd = approval.MaximumRequestUsd, CostCounters = costs,
            ConcurrencyCounters = concurrency.ToArray(), CreatedAtUtc = now
        }, cancellationToken: ct);
    }
    private async Task ReserveCounterAsync(IClientSessionHandle session, string id, decimal amount, decimal limit, CancellationToken ct)
    {
        await budgetCounters.UpdateOneAsync(session, c => c.Id == id,
            Builders<GenerationBudgetCounter>.Update.SetOnInsert(c => c.Used, 0), new UpdateOptions { IsUpsert = true }, ct);
        var result = await budgetCounters.UpdateOneAsync(session, c => c.Id == id && c.Used <= limit - amount,
            Builders<GenerationBudgetCounter>.Update.Inc(c => c.Used, amount), cancellationToken: ct);
        if (result.ModifiedCount != 1) throw new ForbiddenFeatureException("Generation budget or concurrency limit reached.");
    }
    public async Task<bool> BeginCostSubmissionAsync(GenerationJob job, CancellationToken ct)
    {
        if (job.Model.Provider == "fixture") return true;
        if (costGuard == null || job.BudgetReservationId != job.Id) return false;
        costGuard.Validate(job.Model, job.Quote, DateTime.UtcNow);
        var current = await models.Find(m => m.Id == job.Model.Id).FirstOrDefaultAsync(ct);
        if (current == null || !current.Enabled || !current.ProviderEnabled || current.Lifecycle != "ACTIVE" ||
            current.Version != job.Model.Version || current.Pricing.Revision != job.Quote.PricingRevision) return false;
        costGuard.Validate(current, job.Quote, DateTime.UtcNow);
        using var session = await client.StartSessionAsync(cancellationToken: ct);
        var now = DateTime.UtcNow;
        return await session.WithTransactionAsync(async (s, token) => {
            var reservation = await budgetReservations.UpdateOneAsync(s, r => r.Id == job.Id && r.Owner == job.UserId && !r.Attempted && !r.Closed,
                Builders<GenerationBudgetReservation>.Update.Set(r => r.Attempted, true), cancellationToken: token);
            if (reservation.ModifiedCount != 1) return false;
            var marked = await jobs.UpdateOneAsync(s, j => j.Id == job.Id && j.Lease == job.Lease && j.LeaseUntil > now &&
                j.Status == GenerationStatus.Starting && j.SubmissionAttemptedAtUtc == null,
                Builders<GenerationJob>.Update.Set(j => j.SubmissionAttemptedAtUtc, now)
                    .Push(j => j.Journal, new GenerationJournalEntry("CostBudgetPostAuthorized", now)), cancellationToken: token);
            if (marked.ModifiedCount != 1) throw new ConflictAppException("Submission lease lost.");
            return true;
        }, cancellationToken: ct);
    }
    private async Task CloseLaunchBudgetAsync(IClientSessionHandle session, GenerationJob job, CancellationToken ct)
    {
        if (job.BudgetReservationId == null) return;
        var reservation = await budgetReservations.FindOneAndUpdateAsync(session, Builders<GenerationBudgetReservation>.Filter.Where(r => r.Id == job.Id && !r.Closed),
            Builders<GenerationBudgetReservation>.Update.Set(r => r.Closed, true),
            new FindOneAndUpdateOptions<GenerationBudgetReservation> { ReturnDocument = ReturnDocument.Before }, ct);
        if (reservation == null) throw new ConflictAppException("Budget settlement missing.");
        foreach (var id in reservation.ConcurrencyCounters)
            await budgetCounters.UpdateOneAsync(session, c => c.Id == id && c.Used >= 1,
                Builders<GenerationBudgetCounter>.Update.Inc(c => c.Used, -1), cancellationToken: ct);
        // User refunds do not imply a provider refund. Unknown/attempted cost stays encumbered.
        if (!reservation.Attempted)
            foreach (var id in reservation.CostCounters)
                await budgetCounters.UpdateOneAsync(session, c => c.Id == id && c.Used >= reservation.Usd,
                    Builders<GenerationBudgetCounter>.Update.Inc(c => c.Used, -reservation.Usd), cancellationToken: ct);
    }
}
