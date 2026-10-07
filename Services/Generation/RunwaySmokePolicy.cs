using System.Security.Cryptography;
using Imagino.Api.Errors;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Imagino.Api.Services.Generation;

public static class RunwaySmokePolicy
{
    public const string RunId = "runway-grok-lite-single-video-20261007";
    public const string E2eRunId = "runway-grok-lite-e2e-confirmation-20261007";
    public const string ModelId = "runway-fast-video-20261007";
    public const string NativeModel = "grok_imagine_1_5_lite";
    public const string Version = "2026-10-07.1";
    public const string SourceAssetId = "6ac4068a985c143f201d8f15";
    public const string SourceSha256 = "1180fb5b6efd81464a799a2e1fc687ab6a044b21cf0b50ea5d11f041bd36195d";
    public const decimal CostUsd = 0.16m;
    public const decimal CeilingUsd = 0.25m;
    public static readonly DateTime ExpiresAtUtc = new(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc);
    public const string Prompt = "The camera makes a slow, elegant push-in toward the cobalt blue perfume bottle. Soft natural highlights move across the glass while the surrounding plants shift gently in a light breeze. Preserve the bottle shape, blue color, gold band and overall product identity. Premium luxury product commercial, subtle realistic motion, stable composition, no text.";
    public static bool AllowsModel(GenerationModel m) => m.Id == ModelId && m.Provider == "runway" &&
        m.ProviderModel == NativeModel && m.MediaType == "video" && m.Version == Version;
    public static void ValidateSource(string owner, GenerationJob? source, byte[] bytes)
    {
        if (owner != BflHomologationPolicy.OwnerId || source?.Id != SourceAssetId || source.UserId != owner ||
            source.Status != GenerationStatus.Completed || source.CreditState != CreditState.Charged || source.Model.MediaType != "image" ||
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() != SourceSha256)
            throw new ForbiddenFeatureException("Source is outside the controlled video authorization.");
    }
    public static void ValidateRequest(string owner, GenerationModel m, ValidatedGeneration input, DateTime? now = null)
    {
        if (owner != BflHomologationPolicy.OwnerId || !AllowsModel(m) || (now ?? DateTime.UtcNow) >= ExpiresAtUtc ||
            input.Prompt != Prompt || input.Settings.Count != 2 || input.Settings.GetValueOrDefault("duration") != "5" ||
            input.Settings.GetValueOrDefault("resolution") != "720p" || input.Inputs.Count != 1 ||
            input.Inputs[0].Role != "firstFrame" || input.Inputs[0].SourceAssetId != SourceAssetId ||
            Convert.ToHexString(SHA256.HashData(GenerationPolicy.ValidatePngInput(input.Inputs[0].Data))).ToLowerInvariant() != SourceSha256)
            throw new ForbiddenFeatureException("Request is outside the one-video Runway authorization.");
        var quote = GenerationPolicy.Quote(m, input, now ?? DateTime.UtcNow);
        if (quote.ProviderCostEstimateUsd != CostUsd || quote.ImaginoCostEstimateUsd != 0.186m || quote.Credits != 54 ||
            m.Pricing.Revision != Version || m.Pricing.Unit != "second" || m.Pricing.ReferenceUsd != 0.01m ||
            m.Pricing.RatesUsd.Count != 1 || m.Pricing.RatesUsd.GetValueOrDefault("720p") != 0.03m || CostUsd > CeilingUsd)
            throw new ForbiddenFeatureException("Runway pricing differs from the approved projection.");
    }
    public static void ValidateJob(GenerationJob job, DateTime? now = null)
    {
        ValidateRequest(job.UserId, job.Model, new(job.Prompt, job.Settings, job.Inputs), now);
        if (job.RunwayRunId != E2eRunId || job.SourceAssetId != SourceAssetId ||
            job.RequestHash != GenerationPolicy.Fingerprint(job.Model, new(job.Prompt, job.Settings, job.Inputs)) ||
            job.Quote.ProviderCostEstimateUsd != CostUsd || job.Quote.Credits != 54)
            throw new ForbiddenFeatureException("Runway job binding differs from its authorization.");
    }
    public static bool CanReserve(RunwaySmokeLedger l, DateTime now) => l.Id == E2eRunId &&
        l.OwnerId == BflHomologationPolicy.OwnerId && l.SourceAssetId == SourceAssetId && l.SourceSha256 == SourceSha256 &&
        l.ModelId == ModelId && l.BudgetUsd == CeilingUsd && l.EstimatedUsd == CostUsd && now < l.ExpiresAtUtc &&
        !l.Halted && l.State == "Available" && l.JobId == null && l.TaskId == null && l.AttemptCount == 0 && l.SettlementCount == 0;
}
[BsonIgnoreExtraElements]
public sealed class RunwaySmokeLedger
{
    [BsonId] public string Id { get; set; } = RunwaySmokePolicy.E2eRunId;
    public string OwnerId { get; set; } = BflHomologationPolicy.OwnerId;
    public string SourceAssetId { get; set; } = RunwaySmokePolicy.SourceAssetId;
    public string SourceSha256 { get; set; } = RunwaySmokePolicy.SourceSha256;
    public string ModelId { get; set; } = RunwaySmokePolicy.ModelId;
    [BsonRepresentation(BsonType.Decimal128)] public decimal BudgetUsd { get; set; } = RunwaySmokePolicy.CeilingUsd;
    [BsonRepresentation(BsonType.Decimal128)] public decimal EstimatedUsd { get; set; } = RunwaySmokePolicy.CostUsd;
    [BsonRepresentation(BsonType.Decimal128)] public decimal? ObservedUsd { get; set; }
    public DateTime ExpiresAtUtc { get; set; } = RunwaySmokePolicy.ExpiresAtUtc;
    public bool Halted { get; set; }
    public string? HaltReason { get; set; }
    public string State { get; set; } = "Available";
    public string? JobId { get; set; }
    public string? TaskId { get; set; }
    public int AttemptCount { get; set; }
    public int SettlementCount { get; set; }
    public DateTime? AttemptedAtUtc { get; set; }
    public DateTime? AcceptedAtUtc { get; set; }
    public DateTime? SettledAtUtc { get; set; }
}
