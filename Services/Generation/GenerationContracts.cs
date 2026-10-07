using System.Text.Json;
using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Imagino.Api.Services.Generation;

public enum GenerationStatus { Queued, Starting, Processing, Completed, Failed, Cancelled }
public enum CreditState { Reserved, Charged, Refunded }
public sealed class GenerationSettings
{
    public bool Enabled { get; set; }
    public bool SeedStagingCatalog { get; set; }
    public bool StagingFixtureEnabled { get; set; }
    public bool PaidGenerationEnabled { get; set; }
    public bool BflHomologationEnabled { get; set; }
    public bool OpenAiHomologationEnabled { get; set; }
    public bool OpenAiSingleSmokeEnabled { get; set; }
    public bool RunwayIntegrationEnabled { get; set; }
    public bool RunwayRealSmokeEnabled { get; set; }
    public int StagingFixtureDelaySeconds { get; set; }
    public string BflApiKey { get; set; } = "";
    public string GeminiApiKey { get; set; } = "";
    public string OpenAiApiKey { get; set; } = "";
    public string RunwayApiKey { get; set; } = "";
}
public sealed record GenerationField(string Key, string Label, string Type, string DefaultValue, string[] Options);
public sealed record GenerationInputSchema(string Role, string Label, int MaxCount, bool Required = false, bool OwnedAssetOnly = false);
public sealed record GenerationRule(string WhenKey, string WhenValue, string RequireKey, string[] AllowedValues);
public sealed class GenerationPricing
{
    public string Revision { get; set; } = "2026-10-02.1";
    public string Source { get; set; } = "";
    public string Unit { get; set; } = "image";
    public Dictionary<string, decimal> RatesUsd { get; set; } = new();
    public decimal ExtraMegapixelUsd { get; set; }
    public decimal ReferenceUsd { get; set; }
    public decimal OverheadUsd { get; set; } = 0.002m;
    public decimal RiskMultiplier { get; set; } = 1.10m;
    public decimal CreditValueUsd { get; set; } = 0.01m;
    public decimal TargetMargin { get; set; } = 0.65m;
}
[BsonIgnoreExtraElements]
public sealed class GenerationModel
{
    [BsonId] public string Id { get; set; } = "";
    public string Version { get; set; } = "2026-10-02.1";
    public string DisplayName { get; set; } = "";
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";
    public string MediaType { get; set; } = "image";
    public string Provider { get; set; } = "";
    public string ProviderModel { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public bool ProviderEnabled { get; set; } = true;
    public string Lifecycle { get; set; } = "ACTIVE";
    public string Availability { get; set; } = "approval_required";
    public string[] Capabilities { get; set; } = Array.Empty<string>();
    public List<GenerationField> Fields { get; set; } = new();
    public List<GenerationInputSchema> Inputs { get; set; } = new();
    public List<GenerationRule> Rules { get; set; } = new();
    public GenerationPricing Pricing { get; set; } = new();
    public int TimeoutSeconds { get; set; } = 600;
    public int SortOrder { get; set; }
}
public sealed record GenerationInput(string Role, string Data, string? SourceAssetId = null);
public sealed record GenerationJournalEntry(string Stage, DateTime AtUtc);
public sealed class GenerationRequest
{
    public string ModelId { get; set; } = "";
    public string Prompt { get; set; } = "";
    public Dictionary<string, JsonElement> Settings { get; set; } = new();
    public List<GenerationInput> Inputs { get; set; } = new();
    public string? QuoteId { get; set; }
}
public sealed record GenerationQuote(string QuoteId, string ModelId, string Version, int Credits,
    decimal ProviderCostEstimateUsd, decimal ImaginoCostEstimateUsd, string PricingRevision,
    DateTime ExpiresAt, Dictionary<string, string> Settings);
public sealed record ValidatedGeneration(string Prompt, Dictionary<string, string> Settings, List<GenerationInput> Inputs);
[BsonIgnoreExtraElements]
public sealed class GenerationJob
{
    [BsonId, BsonRepresentation(BsonType.ObjectId)] public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
    public string UserId { get; set; } = "";
    public string IdempotencyKey { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public GenerationModel Model { get; set; } = new();
    public string Prompt { get; set; } = "";
    public Dictionary<string, string> Settings { get; set; } = new();
    [JsonIgnore] public List<GenerationInput> Inputs { get; set; } = new();
    public GenerationQuote Quote { get; set; } = default!;
    [BsonRepresentation(BsonType.String)] public GenerationStatus Status { get; set; } = GenerationStatus.Queued;
    [BsonRepresentation(BsonType.String)] public CreditState CreditState { get; set; } = CreditState.Reserved;
    public string? ProviderJobId { get; set; }
    [JsonIgnore] public string? PollingUrl { get; set; }
    [JsonIgnore] public string? Lease { get; set; }
    public DateTime? LeaseUntil { get; set; }
    public DateTime NextPollAt { get; set; } = DateTime.UtcNow;
    public int PollFailures { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime DeadlineAt { get; set; }
    public string? OutputUrl { get; set; }
    public string? ErrorCode { get; set; }
    public decimal? ProviderReportedCostUsd { get; set; }
    public double? ProviderAcceptanceLatencyMs { get; set; }
    public int? BflHomologationCall { get; set; }
    public int? OpenAiHomologationCall { get; set; }
    public string? OpenAiRunId { get; set; }
    public string? RunwayRunId { get; set; }
    public string? SourceAssetId { get; set; }
    public string? ProviderStatus { get; set; }
    public DateTime? ProviderRunningAtUtc { get; set; }
    public DateTime? ProviderReadyAtUtc { get; set; }
    public int ProviderPollCount { get; set; }
    public double? PollDelaySeconds { get; set; }
    public double? ProviderDecodeLatencyMs { get; set; }
    public int? ProviderHttpStatus { get; set; }
    public GenerationUsage? ProviderUsage { get; set; }
    public DateTime? SynchronousResponseAtUtc { get; set; }
    public GenerationOutputMetrics? OutputMetrics { get; set; }
    public List<GenerationJournalEntry> Journal { get; set; } = new();
}
public sealed record GenerationJobView(string Id, string ModelId, string DisplayName, string MediaType,
    string Status, string CreditState, int Credits, string Prompt, Dictionary<string, string> Settings,
    string? OutputUrl, string? ErrorCode, DateTime CreatedAt, DateTime UpdatedAt)
{
    public static GenerationJobView From(GenerationJob j) => new(j.Id, j.Model.Id, j.Model.DisplayName,
        j.Model.MediaType, j.Status.ToString(), j.CreditState.ToString(), j.Quote.Credits, j.Prompt,
        j.Settings, j.OutputUrl, j.ErrorCode, j.CreatedAt, j.UpdatedAt);
}
public sealed record GenerationOutputMetrics(DateTime ProviderReadyAtUtc, DateTime DownloadStartedAtUtc,
    DateTime DownloadFinishedAtUtc, DateTime StoredAtUtc, int Bytes, string Format, int Width, int Height,
    double? ValidationLatencyMs = null, double? StorageLatencyMs = null, double? DurationSeconds = null, string? Sha256 = null);
public sealed record ProviderResult(string? JobId, string? PollingUrl, bool Completed = false,
    byte[]? Bytes = null, string? OutputUrl = null, string? ErrorCode = null, decimal? CostUsd = null,
    double? AcceptanceLatencyMs = null, GenerationUsage? Usage = null, double? DecodeLatencyMs = null,
    int? HttpStatus = null, string? ProviderStatus = null, decimal? EstimatedCostUsd = null);
public sealed record GenerationUsage(long TextInputTokens, long ImageInputTokens, long ImageOutputTokens,
    long InputTokens, long OutputTokens, long TotalTokens, long CachedInputTokens, string PricingRevision);
public interface IGenerationProvider
{
    string Name { get; }
    bool IsConfigured { get; }
    Task<ProviderResult> StartAsync(GenerationJob job, CancellationToken ct);
    Task<ProviderResult> PollAsync(GenerationJob job, CancellationToken ct);
}
public interface IGenerationRepository
{
    Task<RunwaySmokeLedger?> RunwayLedgerAsync(CancellationToken ct) => Task.FromResult<RunwaySmokeLedger?>(null);
    Task<bool> BeginRunwaySubmissionAsync(GenerationJob job, CancellationToken ct) => Task.FromResult(false);
    Task RecordRunwayPollAsync(GenerationJob job, ProviderResult result, CancellationToken ct) => Task.CompletedTask;
    Task<OpenAiHomologationLedger?> SingleSmokeLedgerAsync(CancellationToken ct) => Task.FromResult<OpenAiHomologationLedger?>(null);
    Task InitializeAsync(IEnumerable<GenerationModel> seed, CancellationToken ct);
    Task<List<GenerationModel>> CatalogAsync(CancellationToken ct);
    Task<GenerationJob?> FindByKeyAsync(string userId, string key, CancellationToken ct);
    Task<GenerationJob?> GetAsync(string id, string userId, CancellationToken ct);
    Task<List<GenerationJob>> HistoryAsync(string userId, CancellationToken ct);
    Task<GenerationJob> ReserveAsync(GenerationJob job, CancellationToken ct);
    Task<GenerationJob?> ClaimAsync(CancellationToken ct);
    Task BindAsync(GenerationJob job, ProviderResult result, CancellationToken ct);
    Task<bool> BeginBflSubmissionAsync(GenerationJob job, CancellationToken ct);
    Task<bool> BeginOpenAiSubmissionAsync(GenerationJob job, CancellationToken ct);
    Task RecordSynchronousResultAsync(GenerationJob job, ProviderResult result, CancellationToken ct);
    Task DeferAsync(GenerationJob job, bool failedPoll, CancellationToken ct);
    Task<bool> SettleAsync(GenerationJob job, GenerationStatus status, string? url, string? error, CancellationToken ct);
    Task<bool> CancelAsync(string id, string userId, CancellationToken ct);
}
