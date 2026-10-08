using Imagino.Api.Errors;
using Microsoft.Extensions.Options;
using MongoDB.Bson.Serialization.Attributes;

namespace Imagino.Api.Services.Generation;

public sealed class GenerationCostSettings
{
    public bool EmergencyStop { get; set; } = true;
    public Dictionary<string, ProviderBudget> Providers { get; set; } = new();
    public Dictionary<string, ModelCostApproval> Models { get; set; } = new();
    public int UserDailyCredits { get; set; }
    public int UserConcurrentJobs { get; set; }
    public int UserConcurrentVideos { get; set; }
}
public sealed class ProviderBudget
{
    public bool Enabled { get; set; }
    public decimal DailyUsd { get; set; }
    public decimal MonthlyUsd { get; set; }
}
public sealed class ModelCostApproval
{
    public bool LaunchEnabled { get; set; }
    public string PricingVersion { get; set; } = "";
    public string ModelVersion { get; set; } = "";
    public string Provider { get; set; } = "";
    public string ProviderModel { get; set; } = "";
    public string CostBasis { get; set; } = "";
    public DateTime ReviewedAtUtc { get; set; }
    public DateTime ValidUntilUtc { get; set; }
    public decimal ExpectedRequestUsd { get; set; }
    public decimal MaximumRequestUsd { get; set; }
    public decimal AllowedVariance { get; set; }
    public decimal DailyUsd { get; set; }
}
public sealed class GenerationCostGuard(IOptionsMonitor<GenerationCostSettings> options)
{
    public GenerationCostSettings Current => options.CurrentValue;
    public ModelCostApproval Validate(GenerationModel model, GenerationQuote? quote, DateTime now)
    {
        var settings = Current;
        if (settings.EmergencyStop || !settings.Providers.TryGetValue(model.Provider, out var provider) || !provider.Enabled ||
            provider.DailyUsd <= 0 || provider.MonthlyUsd <= 0 || settings.UserDailyCredits <= 0 ||
            settings.UserConcurrentJobs <= 0 || settings.UserConcurrentVideos <= 0 ||
            !settings.Models.TryGetValue(model.Id, out var approval) || !approval.LaunchEnabled || approval.DailyUsd <= 0 ||
            approval.Provider != model.Provider || approval.ProviderModel != model.ProviderModel || approval.ModelVersion != model.Version ||
            approval.PricingVersion != model.Pricing.Revision || string.IsNullOrWhiteSpace(approval.CostBasis) ||
            approval.ReviewedAtUtc > now || approval.ReviewedAtUtc == default || approval.ValidUntilUtc <= now ||
            approval.ExpectedRequestUsd <= 0 || approval.MaximumRequestUsd < approval.ExpectedRequestUsd ||
            approval.AllowedVariance is < 0 or > 1 ||
            quote != null && (quote.PricingRevision != approval.PricingVersion || quote.Version != approval.ModelVersion ||
                quote.ProviderCostEstimateUsd <= 0 || quote.ProviderCostEstimateUsd > approval.MaximumRequestUsd ||
                quote.ProviderCostEstimateUsd > approval.ExpectedRequestUsd * (1 + approval.AllowedVariance)))
            throw new ForbiddenFeatureException("Generation is paused pending cost and budget review.");
        return approval;
    }
}
[BsonIgnoreExtraElements]
public sealed class GenerationBudgetCounter
{
    [BsonId] public string Id { get; set; } = "";
    public decimal Used { get; set; }
}
[BsonIgnoreExtraElements]
public sealed class GenerationBudgetReservation
{
    [BsonId] public string Id { get; set; } = "";
    public string Owner { get; set; } = "";
    public decimal Usd { get; set; }
    public string[] CostCounters { get; set; } = Array.Empty<string>();
    public string[] ConcurrencyCounters { get; set; } = Array.Empty<string>();
    public bool Attempted { get; set; }
    public bool Closed { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
