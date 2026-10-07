using System.Security.Claims;
using Imagino.Api.Errors;
using Imagino.Api.Services.Generation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Imagino.Api.Controllers;

[ApiController, Route("api/generation")]
[RequestSizeLimit(12 * 1024 * 1024)]
public sealed class GenerationController(IGenerationRepository repository, GenerationService service,
    IGenerationOutputStore storage, IOptions<GenerationSettings> options) : ControllerBase
{
    private string Owner => User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new ValidationAppException("Authenticated owner missing.");
    [HttpGet("runway/single-video/output-inspection"), Authorize]
    public async Task<IActionResult> RunwayOutputInspection([FromServices] RunwayOutputInspection inspection, CancellationToken ct)
    {
        Response.Headers.CacheControl = "private, no-store";
        var result = await inspection.InspectAsync(Owner, ct);
        return result == null ? NotFound() : Ok(result);
    }
    [HttpGet("runway/single-video/proof"), Authorize]
    public async Task<IActionResult> RunwayProof(CancellationToken ct)
    {
        Response.Headers.CacheControl = "private, no-store";
        if (!options.Value.Enabled || !options.Value.RunwayIntegrationEnabled || Owner != BflHomologationPolicy.OwnerId) return NotFound();
        var ledger = await repository.RunwayLedgerAsync(ct);
        var job = ledger?.JobId == null ? null : await repository.GetAsync(ledger.JobId, Owner, ct);
        return Ok(new { options.Value.PaidGenerationEnabled, options.Value.RunwayRealSmokeEnabled,
            credentialsConfigured = !string.IsNullOrWhiteSpace(options.Value.RunwayApiKey), ledger,
            job = job == null ? null : new { job.Id, job.RunwayRunId, job.SourceAssetId, job.ProviderJobId,
                status = job.Status.ToString(), creditState = job.CreditState.ToString(), job.ProviderStatus, job.ProviderReportedCostUsd,
                job.ProviderAcceptanceLatencyMs, job.ProviderRunningAtUtc, job.ProviderReadyAtUtc, job.ProviderPollCount,
                job.OutputMetrics, job.Journal, job.CreatedAt, job.UpdatedAt, job.Quote }
        });
    }
    [HttpGet("openai/single-smoke/proof"), Authorize]
    public async Task<IActionResult> SingleSmokeProof(CancellationToken ct)
    {
        Response.Headers.CacheControl = "private, no-store";
        if (!options.Value.Enabled || !options.Value.OpenAiHomologationEnabled || Owner != BflHomologationPolicy.OwnerId) return NotFound();
        var ledger = await repository.SingleSmokeLedgerAsync(ct);
        if (ledger == null) return NotFound();
        var job = await repository.FindByKeyAsync(Owner, OpenAiSingleSmokePolicy.Key, ct);
        return Ok(new { options.Value.PaidGenerationEnabled, options.Value.OpenAiSingleSmokeEnabled,
            hardRequestCostCap = false, ledger, job = job == null ? null : new {
                job.Id, job.OpenAiRunId, status = job.Status.ToString(), creditState = job.CreditState.ToString(), job.ProviderHttpStatus, job.ProviderUsage,
                job.ProviderReportedCostUsd, job.ProviderAcceptanceLatencyMs, job.ProviderDecodeLatencyMs,
                job.SynchronousResponseAtUtc, job.OutputMetrics, job.CreatedAt, job.UpdatedAt, job.Journal,
                reservedCredits = job.Quote.Credits,
                experimentalCreditsFromActual = job.ProviderReportedCostUsd == null ? (int?)null :
                    OpenAiImagePricing.ExperimentalCredits(job.ProviderReportedCostUsd.Value, job.Model.Pricing)
            } });
    }
    [HttpGet("openai/preflight"), Authorize]
    public async Task<IActionResult> OpenAiPreflight([FromServices] OpenAiModelAccessPreflight preflight, CancellationToken ct)
    {
        Response.Headers.CacheControl = "private, no-store";
        var result = await preflight.CheckAsync(Owner, ct);
        return result == null ? NotFound() : Ok(result);
    }
    [HttpGet("catalog"), AllowAnonymous]
    public async Task<IActionResult> Catalog(CancellationToken ct)
    {
        if (!options.Value.Enabled) return NotFound();
        var models = await repository.CatalogAsync(ct);
        return Ok(new { revision = GenerationCatalog.Revision, models = models.Select(m => new {
            m.Id, m.Version, m.DisplayName, m.Category, m.Description, m.MediaType, m.ProviderModel,
            m.Capabilities, m.Fields, m.Inputs, m.Rules,
            lifecycle = GenerationLifecycle.RequiresMigration(m, DateTime.UtcNow) ? "COMPATIBILITY" : m.Lifecycle,
            retirementAt = GenerationLifecycle.RetirementAt(m), availability = service.Availability(m),
            startingCredits = GenerationPolicy.CatalogStartingCredits(m, DateTime.UtcNow)
        }) });
    }
    [HttpPost("quote"), Authorize]
    public async Task<IActionResult> Quote(GenerationRequest request, CancellationToken ct) =>
        !options.Value.Enabled ? NotFound() : Ok(await service.QuoteAsync(request, ct, Owner));
    [HttpPost("jobs"), Authorize]
    public async Task<IActionResult> Create(GenerationRequest request, CancellationToken ct)
    {
        if (!options.Value.Enabled) return NotFound();
        var job = await service.CreateAsync(Owner, Request.Headers["Idempotency-Key"].ToString(), request, ct);
        return AcceptedAtAction(nameof(Get), new { id = job.Id }, GenerationJobView.From(job));
    }
    [HttpGet("jobs"), Authorize]
    public async Task<IActionResult> History(CancellationToken ct) =>
        !options.Value.Enabled ? NotFound() : Ok((await repository.HistoryAsync(Owner, ct)).Select(GenerationJobView.From));
    [HttpGet("jobs/{id}"), Authorize]
    public async Task<IActionResult> Get(string id, CancellationToken ct)
    {
        if (!options.Value.Enabled) return NotFound();
        var job = await repository.GetAsync(id, Owner, ct);
        return job == null ? NotFound() : Ok(GenerationJobView.From(job));
    }
    [HttpPost("jobs/{id}/cancel"), Authorize]
    public async Task<IActionResult> Cancel(string id, CancellationToken ct)
    {
        if (!options.Value.Enabled) return NotFound();
        var job = await repository.GetAsync(id, Owner, ct);
        if (job == null) return NotFound();
        if (job.Status == GenerationStatus.Cancelled) return Ok(GenerationJobView.From(job));
        if (!await repository.CancelAsync(id, Owner, ct)) throw new ConflictAppException("Only queued jobs can be cancelled before provider submission.");
        return Ok(GenerationJobView.From((await repository.GetAsync(id, Owner, ct))!));
    }
    [HttpGet("jobs/{id}/download"), Authorize]
    public async Task<IActionResult> Download(string id, CancellationToken ct)
    {
        if (!options.Value.Enabled) return NotFound();
        var job = await repository.GetAsync(id, Owner, ct);
        if (job?.Status != GenerationStatus.Completed) return NotFound();
        Response.Headers.CacheControl = "private, no-store";
        var output = await storage.DownloadAsync(job, ct);
        return File(output.Bytes, output.ContentType);
    }
}
