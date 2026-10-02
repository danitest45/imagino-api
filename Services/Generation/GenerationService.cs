using System.Text.RegularExpressions;
using Imagino.Api.Errors;
using Microsoft.Extensions.Options;

namespace Imagino.Api.Services.Generation;

public sealed class GenerationService(IGenerationRepository repository, IEnumerable<IGenerationProvider> providers,
    IOptions<GenerationSettings> options)
{
    public async Task<GenerationModel> ModelAsync(string id, CancellationToken ct) =>
        (await repository.CatalogAsync(ct)).FirstOrDefault(m => m.Id == id) ?? throw new ValidationAppException("Unknown model.");
    public string Availability(GenerationModel model)
    {
        if (!model.Enabled || model.Lifecycle != "ACTIVE" || !model.ProviderEnabled) return "disabled";
        if (model.Provider == "fixture") return options.Value.StagingFixtureEnabled ? "synthetic_demo" : "disabled";
        if (!options.Value.PaidGenerationEnabled) return "approval_required";
        return providers.Any(p => p.Name == model.Provider && p.IsConfigured) ? "ready" : "credentials_required";
    }
    public async Task<GenerationQuote> QuoteAsync(GenerationRequest request, CancellationToken ct)
    {
        var model = await ModelAsync(request.ModelId, ct);
        return GenerationPolicy.Quote(model, GenerationPolicy.Validate(model, request), DateTime.UtcNow);
    }
    public async Task<GenerationJob> CreateAsync(string userId, string? key, GenerationRequest request, CancellationToken ct)
    {
        if (key == null || !Regex.IsMatch(key, "^[A-Za-z0-9_-]{16,100}$")) throw new ValidationAppException("A 16–100 character Idempotency-Key header is required.");
        var model = await ModelAsync(request.ModelId, ct);
        var input = GenerationPolicy.Validate(model, request);
        var hash = GenerationPolicy.Fingerprint(model, input);
        var existing = await repository.FindByKeyAsync(userId, key, ct);
        if (existing != null) return existing.RequestHash == hash ? existing : throw new ConflictAppException("Idempotency key already belongs to another request.");
        if (Availability(model) is not ("ready" or "synthetic_demo"))
            throw new ForbiddenFeatureException("Generation requires provider credentials and explicit spending approval.");
        GenerationPolicy.ValidateQuote(request.QuoteId, hash, DateTime.UtcNow);
        var quote = GenerationPolicy.Quote(model, input, DateTime.UtcNow);
        return await repository.ReserveAsync(new GenerationJob {
            UserId = userId, IdempotencyKey = key, RequestHash = hash, Model = model, Prompt = input.Prompt,
            Settings = input.Settings, Inputs = input.Inputs, Quote = quote,
            DeadlineAt = DateTime.UtcNow.AddSeconds(model.TimeoutSeconds)
        }, ct);
    }
}
