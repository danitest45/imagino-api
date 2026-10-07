using System.Text.RegularExpressions;
using Imagino.Api.Errors;
using Microsoft.Extensions.Options;

namespace Imagino.Api.Services.Generation;

public sealed class GenerationService(IGenerationRepository repository, IEnumerable<IGenerationProvider> providers,
    IOptions<GenerationSettings> options, IGenerationOutputStore? storage = null, TimeProvider? clock = null)
{
    public async Task<GenerationModel> ModelAsync(string id, CancellationToken ct) =>
        (await repository.CatalogAsync(ct)).FirstOrDefault(m => m.Id == id) ?? throw new ValidationAppException("Unknown model.");
    public string Availability(GenerationModel model, DateTime? now = null)
    {
        var currentTime = now ?? clock?.GetUtcNow().UtcDateTime ?? DateTime.UtcNow;
        if (GenerationLifecycle.IsRetired(model, currentTime)) return "retired";
        if (GenerationLifecycle.RequiresMigration(model, currentTime)) return "migration_required";
        if (!model.Enabled || model.Lifecycle != "ACTIVE" || !model.ProviderEnabled) return "disabled";
        if (model.Provider == "fixture") return options.Value.StagingFixtureEnabled ? "synthetic_demo" : "disabled";
        if (options.Value.RunwayRealSmokeEnabled && model.Provider != "runway") return "approval_required";
        if (model.Provider == "runway")
            return options.Value.RunwayIntegrationEnabled && options.Value.RunwayRealSmokeEnabled && options.Value.PaidGenerationEnabled &&
                !options.Value.OpenAiSingleSmokeEnabled && RunwaySmokePolicy.AllowsModel(model) && currentTime < RunwaySmokePolicy.ExpiresAtUtc
                ? providers.Any(p => p.Name == model.Provider && p.IsConfigured) ? "ready" : "credentials_required" : "approval_required";
        if (options.Value.OpenAiSingleSmokeEnabled && !OpenAiSingleSmokePolicy.AllowsModel(model)) return "approval_required";
        if (model.Provider == "openai" && (!options.Value.OpenAiHomologationEnabled || !options.Value.OpenAiSingleSmokeEnabled ||
            !OpenAiSingleSmokePolicy.AllowsModel(model) || currentTime >= OpenAiHomologationPolicy.ExpiresAtUtc)) return "approval_required";
        if (options.Value.BflHomologationEnabled && model.Provider != "openai" && (!BflHomologationPolicy.AllowsModel(model) || currentTime >= BflHomologationPolicy.ExpiresAtUtc))
            return "approval_required";
        if (!options.Value.PaidGenerationEnabled) return "approval_required";
        return providers.Any(p => p.Name == model.Provider && p.IsConfigured) ? "ready" : "credentials_required";
    }
    public async Task<GenerationQuote> QuoteAsync(GenerationRequest request, CancellationToken ct, string? owner = null)
    {
        var model = await ModelAsync(request.ModelId, ct);
        request = await ResolveOwnedInputsAsync(owner ?? "", model, request, ct);
        var input = GenerationPolicy.Validate(model, request);
        if (model.Provider == "runway")
        {
            if (Availability(model) != "ready") throw new ForbiddenFeatureException("The one-video Runway authorization is closed.");
            RunwaySmokePolicy.ValidateRequest(owner ?? "", model, input);
        }
        if (model.Provider == "openai")
        {
            if (!options.Value.OpenAiSingleSmokeEnabled || !options.Value.PaidGenerationEnabled)
                throw new ForbiddenFeatureException("The one-call Flare smoke authorization is closed.");
            OpenAiSingleSmokePolicy.ValidateRequest(owner ?? "", model, input);
        }
        var quote = GenerationPolicy.Quote(model, input, DateTime.UtcNow, options.Value.OpenAiSingleSmokeEnabled);
        if (options.Value.BflHomologationEnabled && model.Provider != "fixture" && model.Provider != "openai" && model.Provider != "runway")
            BflHomologationPolicy.Validate(owner ?? "", model, input, quote, DateTime.UtcNow);
        return quote;
    }
    public async Task<GenerationJob> CreateAsync(string userId, string? key, GenerationRequest request, CancellationToken ct)
    {
        if (key == null || !Regex.IsMatch(key, "^[A-Za-z0-9_-]{16,100}$")) throw new ValidationAppException("A 16–100 character Idempotency-Key header is required.");
        var model = await ModelAsync(request.ModelId, ct);
        request = await ResolveOwnedInputsAsync(userId, model, request, ct);
        var input = GenerationPolicy.Validate(model, request);
        var hash = GenerationPolicy.Fingerprint(model, input);
        var existing = await repository.FindByKeyAsync(userId, key, ct);
        if (existing != null) return existing.RequestHash == hash ? existing : throw new ConflictAppException("Idempotency key already belongs to another request.");
        if (Availability(model) is not ("ready" or "synthetic_demo"))
            throw new ForbiddenFeatureException("Generation requires provider credentials and explicit spending approval.");
        GenerationPolicy.ValidateQuote(request.QuoteId, hash, DateTime.UtcNow);
        var quote = GenerationPolicy.Quote(model, input, DateTime.UtcNow, options.Value.OpenAiSingleSmokeEnabled);
        var job = new GenerationJob {
            UserId = userId, IdempotencyKey = key, RequestHash = hash, Model = model, Prompt = input.Prompt,
            Settings = input.Settings, Inputs = input.Inputs, Quote = quote,
            Journal = new() { new("QueuedReserved", DateTime.UtcNow) },
            DeadlineAt = DateTime.UtcNow.AddSeconds(model.TimeoutSeconds)
        };
        if (model.Provider == "openai")
        {
            job.OpenAiRunId = OpenAiSingleSmokePolicy.RunId;
            OpenAiSingleSmokePolicy.ValidateJob(job);
        }
        else if (model.Provider == "runway")
        {
            job.RunwayRunId = RunwaySmokePolicy.RunId;
            job.SourceAssetId = input.Inputs.Single().SourceAssetId;
            RunwaySmokePolicy.ValidateJob(job);
        }
        else if (options.Value.BflHomologationEnabled && model.Provider != "fixture") BflHomologationPolicy.ValidateJob(job);
        return await repository.ReserveAsync(job, ct);
    }
    private async Task<GenerationRequest> ResolveOwnedInputsAsync(string owner, GenerationModel model, GenerationRequest request, CancellationToken ct)
    {
        if (request.Inputs == null) throw new ValidationAppException("Inputs cannot be null.");
        var resolved = new List<GenerationInput>();
        foreach (var input in request.Inputs)
        {
            if (input == null) throw new ValidationAppException("Invalid input.");
            var spec = model.Inputs.FirstOrDefault(i => i.Role == input.Role);
            if (input.SourceAssetId == null) { resolved.Add(input); continue; }
            if (spec?.OwnedAssetOnly != true || storage == null) throw new ValidationAppException("Owned asset input is unsupported.");
            // Never treat a supplied data URI or public R2 URL as ownership proof.
            var source = await repository.GetAsync(input.SourceAssetId, owner, ct);
            if (source?.Status != GenerationStatus.Completed || source.Model.MediaType != "image")
                throw new ForbiddenFeatureException("Source asset is unavailable.");
            var (bytes, contentType) = await storage.DownloadAsync(source, ct);
            if (contentType != "image/png") throw new ValidationAppException("First frame must be a PNG image.");
            if (model.Provider == "runway") RunwaySmokePolicy.ValidateSource(owner, source, bytes);
            resolved.Add(new(input.Role, "data:image/png;base64," + Convert.ToBase64String(bytes), source.Id));
        }
        return new() { ModelId = request.ModelId, Prompt = request.Prompt, Settings = request.Settings, Inputs = resolved, QuoteId = request.QuoteId };
    }
}
