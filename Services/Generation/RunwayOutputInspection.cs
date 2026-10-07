using Microsoft.Extensions.Options;

namespace Imagino.Api.Services.Generation;

// Read-only postmortem for the single failed task. No creation, storage or settlement dependency.
public sealed class RunwayOutputInspection(IGenerationRepository repository, GenerationProviderHttp http,
    IOptions<GenerationSettings> options)
{
    public sealed record Result(string JobId, string TaskId, string ProviderStatus, decimal? CostUsd,
        string? ContentType, string Validation, int? Bytes = null, string? Sha256 = null,
        GeneratedVideoValidator.Metadata? Video = null);

    public async Task<Result?> InspectAsync(string owner, CancellationToken ct)
    {
        var settings = options.Value;
        if (owner != BflHomologationPolicy.OwnerId || !settings.Enabled || !settings.RunwayIntegrationEnabled ||
            settings.PaidGenerationEnabled || settings.RunwayRealSmokeEnabled ||
            string.IsNullOrWhiteSpace(settings.RunwayApiKey) || DateTime.UtcNow >= RunwaySmokePolicy.ExpiresAtUtc) return null;
        var ledger = await repository.RunwayLedgerAsync(ct);
        if (ledger is not { State: "Failed", Halted: true, HaltReason: "invalid_provider_output", AttemptCount: 1, SettlementCount: 1 } ||
            ledger.Id != RunwaySmokePolicy.RunId || ledger.OwnerId != owner || ledger.JobId == null || ledger.TaskId == null) return null;
        var job = await repository.GetAsync(ledger.JobId, owner, ct);
        if (job is not { Status: GenerationStatus.Failed, CreditState: CreditState.Refunded, ProviderStatus: "SUCCEEDED" } ||
            job.RunwayRunId != RunwaySmokePolicy.RunId || job.ProviderJobId != ledger.TaskId ||
            job.Model.Id != RunwaySmokePolicy.ModelId || job.Model.Provider != "runway" ||
            job.SourceAssetId != RunwaySmokePolicy.SourceAssetId) return null;
        var result = await new RunwayGenerationProvider(http, options).PollAsync(job, ct);
        if (!result.Completed || result.OutputUrl == null || result.CostUsd != RunwaySmokePolicy.CostUsd) return null;
        string? mime = null;
        byte[]? bytes = null;
        try
        {
            bytes = await http.DownloadAsync(result.OutputUrl, RunwayGenerationProvider.OutputHosts, null,
                GeneratedVideoValidator.MaxBytes, ct, "video/mp4", value => mime = value);
            var video = GeneratedVideoValidator.Validate(bytes);
            return new(job.Id, ledger.TaskId, result.ProviderStatus!, result.CostUsd, mime, "valid_mp4", bytes.Length,
                Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant(), video);
        }
        catch (InvalidDataException ex)
        {
            // All messages here originate in our bounded download/container validators; no URL/body is returned.
            var validation = ex.Message == "Provider output Content-Type differs from the expected container."
                ? "content_type_mismatch" : bytes == null ? "download_size_or_container_rejected" : "mp4_structure_or_configuration_rejected";
            return new(job.Id, ledger.TaskId, result.ProviderStatus!, result.CostUsd, mime, validation, bytes?.Length,
                bytes == null ? null : Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant());
        }
    }
}
