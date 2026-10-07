using Imagino.Api.Security;
using Microsoft.Extensions.Options;

namespace Imagino.Api.Services.Generation;

public sealed class GenerationProcessor(IGenerationRepository repository, IEnumerable<IGenerationProvider> providers,
    GenerationService service, GenerationProviderHttp http, IGenerationOutputStore storage,
    IOptions<GenerationSettings> options, ILogger<GenerationProcessor> logger)
{
    public async Task ProcessAsync(GenerationJob job, CancellationToken stoppingToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var remaining = job.DeadlineAt - DateTime.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            await FinishAsync(job, GenerationStatus.Failed, null, job.SynchronousResponseAtUtc != null ? "synchronous_result_lost" :
                job.ProviderJobId == null && job.Status == GenerationStatus.Starting ? "submission_unknown" : "generation_timeout", stoppingToken);
            return;
        }
        deadline.CancelAfter(remaining < TimeSpan.FromMinutes(2) ? remaining : TimeSpan.FromMinutes(2));
        var ct = deadline.Token;
        var synchronousResponseReceived = false;
        var providerOutputReady = false;
        var provider = providers.FirstOrDefault(p => p.Name == job.Model.Provider);
        if (provider == null || !provider.IsConfigured)
        {
            if (job.Status == GenerationStatus.Starting) await FinishAsync(job, GenerationStatus.Failed, null, "provider_unavailable", stoppingToken);
            else await repository.DeferAsync(job, true, stoppingToken);
            return;
        }
        try
        {
            if (job.Status == GenerationStatus.Starting)
            {
                var current = await service.ModelAsync(job.Model.Id, ct);
                if (service.Availability(current) is not ("ready" or "synthetic_demo"))
                {
                    await FinishAsync(job, GenerationStatus.Failed, null, "generation_disabled", stoppingToken);
                    return;
                }
                if (options.Value.BflHomologationEnabled && job.Model.Provider == "bfl" &&
                    !await repository.BeginBflSubmissionAsync(job, ct))
                {
                    await FinishAsync(job, GenerationStatus.Failed, null, "bfl_submission_blocked", stoppingToken);
                    return;
                }
                if (job.Model.Provider == "openai" && (!options.Value.OpenAiHomologationEnabled ||
                    !await repository.BeginOpenAiSubmissionAsync(job, ct)))
                {
                    await FinishAsync(job, GenerationStatus.Failed, null, "openai_submission_blocked", stoppingToken);
                    return;
                }
                if (job.Model.Provider == "runway" && !await repository.BeginRunwaySubmissionAsync(job, ct))
                {
                    await FinishAsync(job, GenerationStatus.Failed, null, "runway_submission_blocked", stoppingToken);
                    return;
                }
                // Exactly one application-level POST attempt. No retry for ambiguous submissions.
                var result = await provider.StartAsync(job, ct);
                if (result.Completed)
                {
                    synchronousResponseReceived = true;
                    await repository.RecordSynchronousResultAsync(job, result, ct);
                    if (result.Usage != null)
                        logger.LogInformation("Generation measured job={Job} provider={Provider} model={Model} textInputTokens={Text} imageInputTokens={Image} imageOutputTokens={Output} calculatedUsd={Cost} pricingRevision={Revision} providerLatencyMs={Latency}",
                            job.Id, job.Model.Provider, job.Model.ProviderModel, result.Usage.TextInputTokens, result.Usage.ImageInputTokens,
                            result.Usage.ImageOutputTokens, result.CostUsd, result.Usage.PricingRevision, result.AcceptanceLatencyMs);
                    var isSmoke = job.OpenAiRunId == OpenAiSingleSmokePolicy.RunId;
                    if (job.Model.Provider == "openai" && (result.CostUsd == null || result.Usage == null || (isSmoke
                        ? result.CostUsd > OpenAiSingleSmokePolicy.ObservedCeilingUsd
                        : OpenAiHomologationPolicy.VerifiedMaximumUsd(job.OpenAiHomologationCall ?? 0) is not > 0 ||
                            result.CostUsd > OpenAiHomologationPolicy.VerifiedMaximumUsd(job.OpenAiHomologationCall ?? 0))))
                    {
                        await FinishAsync(job, GenerationStatus.Failed, null, isSmoke ? "observed_ceiling_exceeded" : "cost_bound_exceeded", stoppingToken);
                        return;
                    }
                    if (result.ErrorCode != null) await FinishAsync(job, GenerationStatus.Failed, null, result.ErrorCode, stoppingToken);
                    else await StoreAndFinishAsync(job, result, ct);
                }
                else if (result.ErrorCode != null) await FinishAsync(job, GenerationStatus.Failed, null, result.ErrorCode, stoppingToken);
                else
                {
                    await BindReceivedTaskAsync(job, result, ct);
                    logger.LogInformation("Generation provider bound job={Job} provider={Provider} stage=provider_bound", job.Id, job.Model.Provider);
                }
                return;
            }
            var polled = await provider.PollAsync(job, ct);
            if (job.Model.Provider == "runway")
            {
                await repository.RecordRunwayPollAsync(job, polled, ct);
                if (polled.EstimatedCostUsd > RunwaySmokePolicy.CeilingUsd || polled.CostUsd > RunwaySmokePolicy.CeilingUsd ||
                    (polled.Completed || polled.ErrorCode != null) && polled.CostUsd == null)
                {
                    await FinishAsync(job, GenerationStatus.Failed, null, "provider_cost_unverified_or_exceeded", stoppingToken);
                    return;
                }
            }
            if (polled.ErrorCode != null)
            {
                await FinishAsync(job, polled.ErrorCode == "provider_cancelled" ? GenerationStatus.Cancelled : GenerationStatus.Failed, null, polled.ErrorCode, stoppingToken);
                return;
            }
            if (!polled.Completed)
            {
                await repository.DeferAsync(job, false, ct);
                return;
            }
            providerOutputReady = true;
            await StoreAndFinishAsync(job, polled, ct);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown leaves the lease/binding durable; never dispatch the POST again.
        }
        catch (Exception ex)
        {
            logger.LogWarning("Generation attempt error job={Job} provider={Provider} model={Model} type={ErrorType}",
                job.Id, job.Model.Provider, job.Model.ProviderModel, ex.GetType().Name);
            if (job.Status == GenerationStatus.Starting)
                await FinishAsync(job, GenerationStatus.Failed, null, synchronousResponseReceived
                    ? ex is ArgumentException or InvalidDataException or FormatException ? "invalid_provider_output" : "output_storage_failed"
                    : ex is ProviderCallException p && p.Status is >= 400 and < 500 ? p.Code?.Contains("SAFETY") == true ? "provider_moderated" : "provider_rejected" : "submission_unknown", stoppingToken);
            else if (ex is ArgumentException or InvalidDataException or FormatException)
                await FinishAsync(job, GenerationStatus.Failed, null, "invalid_provider_output", stoppingToken);
            else if (job.Model.Provider == "runway" && providerOutputReady)
                await FinishAsync(job, GenerationStatus.Failed, null, "output_storage_failed", stoppingToken);
            else
            {
                if (ex is ProviderCallException limited && limited.RetryAfterSeconds is > 0)
                    job.PollDelaySeconds = Math.Clamp(limited.RetryAfterSeconds.Value, 5, 3600);
                await repository.DeferAsync(job, true, stoppingToken);
            }
        }
    }
    private async Task BindReceivedTaskAsync(GenerationJob job, ProviderResult result, CancellationToken ct)
    {
        // Retry only persistence of this already received ID, never provider creation.
        for (var attempt = 0; ; attempt++)
        {
            try { await repository.BindAsync(job, result, ct); return; }
            catch (Exception ex) when (job.Model.Provider == "runway" && attempt < 2 &&
                ex is MongoDB.Driver.MongoException or TimeoutException)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250 * (attempt + 1)), ct);
            }
        }
    }
    private async Task StoreAndFinishAsync(GenerationJob job, ProviderResult result, CancellationToken ct)
    {
        var readyAt = job.ProviderReadyAtUtc ?? DateTime.UtcNow;
        var downloadStartedAt = DateTime.UtcNow;
        var bytes = result.Bytes;
        if (bytes == null)
        {
            if (result.OutputUrl == null) throw new InvalidDataException("Missing provider output.");
            var hosts = job.Model.Provider == "runway" ? RunwayGenerationProvider.OutputHosts : job.Model.Provider == "bfl" ? new[] { "*.bfl.ai" } : new[] { "generativelanguage.googleapis.com", "storage.googleapis.com" };
            bytes = await http.DownloadAsync(result.OutputUrl, hosts, job.Model.Provider == "google-veo" ? options.Value.GeminiApiKey : null,
                job.Model.MediaType == "video" ? 100 * 1024 * 1024 : GeneratedImageValidator.MaxBytes, ct,
                job.Model.Provider == "runway" ? "video/mp4" : null);
        }
        var downloadedAt = DateTime.UtcNow;
        var measuredPng = job.Model.Provider == "openai" || options.Value.BflHomologationEnabled && job.Model.Provider == "bfl";
        var measure = System.Diagnostics.Stopwatch.StartNew();
        var dims = measuredPng ? BflHomologationPolicy.ValidateOutput(bytes) : (Width: 0, Height: 0);
        var video = job.Model.Provider == "runway" ? GeneratedVideoValidator.Validate(bytes) : null;
        var validationMs = measure.Elapsed.TotalMilliseconds;
        measure.Restart();
        var url = await storage.StoreAsync(job, bytes, ct);
        if (measuredPng) job.OutputMetrics = new(readyAt, downloadStartedAt, downloadedAt, DateTime.UtcNow, bytes.Length, "png", dims.Width, dims.Height,
            validationMs, measure.Elapsed.TotalMilliseconds);
        if (video != null) job.OutputMetrics = new(readyAt, downloadStartedAt, downloadedAt, DateTime.UtcNow, bytes.Length, "mp4", video.Width, video.Height,
            validationMs, measure.Elapsed.TotalMilliseconds, video.DurationSeconds,
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant());
        logger.LogInformation("Generation output stored job={Job} provider={Provider} bytes={Bytes} stage=output_stored", job.Id, job.Model.Provider, bytes.Length);
        await FinishAsync(job, GenerationStatus.Completed, url, null, ct);
    }
    private async Task FinishAsync(GenerationJob job, GenerationStatus status, string? url, string? error, CancellationToken ct)
    {
        if (await repository.SettleAsync(job, status, url, error, ct))
            logger.LogInformation("Generation settled job={Job} provider={Provider} model={Model} version={Version} status={Status} elapsedMs={Elapsed} estimatedUsd={Cost} credits={Credits}",
                job.Id, job.Model.Provider, job.Model.ProviderModel, job.Model.Version, status,
                (DateTime.UtcNow - job.CreatedAt).TotalMilliseconds, job.Quote.ProviderCostEstimateUsd, job.Quote.Credits);
    }
}
public sealed class GenerationWorker(IServiceProvider services, IOptions<GenerationSettings> options,
    ILogger<GenerationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        using var scope = services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGenerationRepository>();
        await repository.InitializeAsync(options.Value.SeedStagingCatalog ? GenerationCatalog.Seed(options.Value.StagingFixtureEnabled) : Array.Empty<GenerationModel>(), stoppingToken);
        var processor = scope.ServiceProvider.GetRequiredService<GenerationProcessor>();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var job = await repository.ClaimAsync(stoppingToken);
                if (job != null)
                {
                    logger.LogInformation("Generation claimed job={Job} provider={Provider} status={Status} stage=worker_claim", job.Id, job.Model.Provider, job.Status);
                    await processor.ProcessAsync(job, stoppingToken);
                }
                else await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogError("Generation worker iteration failed type={ErrorType}", ex.GetType().Name);
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }
}
