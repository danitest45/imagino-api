using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Imagino.Api.Services.Generation;

// Metadata only: this service has no generation, job, wallet or storage dependency.
public sealed class OpenAiModelAccessPreflight(GenerationProviderHttp http, IOptions<GenerationSettings> options,
    IConfiguration configuration)
{
    public async Task<OpenAiModelAccessReport?> CheckAsync(string owner, CancellationToken ct)
    {
        var settings = options.Value;
        if (!settings.Enabled || !settings.OpenAiHomologationEnabled || settings.PaidGenerationEnabled ||
            configuration["ASPNETCORE_ENVIRONMENT"] != "AIStaging" ||
            configuration["RENDER_SERVICE_ID"] != BflHomologationPolicy.ServiceId ||
            configuration["RENDER_GIT_BRANCH"] != "codex/imagino-ai-revival-v2" ||
            configuration["RENDER_EXTERNAL_HOSTNAME"] != "imagino-api-ai-staging.onrender.com" ||
            owner != BflHomologationPolicy.OwnerId) return null;

        var models = new List<OpenAiModelAccess>();
        foreach (var model in new[] { OpenAiHomologationPolicy.Flare, OpenAiHomologationPolicy.Sunburst })
        {
            if (string.IsNullOrWhiteSpace(settings.OpenAiApiKey))
            {
                models.Add(new(model, null, "credential_missing"));
                continue;
            }
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            try
            {
                using var response = await http.SendAsync(HttpMethod.Get, "https://api.openai.com/v1/models/" + model,
                    "Authorization", "Bearer " + settings.OpenAiApiKey, null, ["api.openai.com"], timeout.Token);
                var root = response.RootElement;
                var matches = root.ValueKind == JsonValueKind.Object &&
                    root.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String && id.GetString() == model &&
                    root.TryGetProperty("object", out var kind) && kind.ValueKind == JsonValueKind.String && kind.GetString() == "model";
                models.Add(new(model, 200, matches ? "metadata_access_confirmed" : "invalid_response"));
            }
            catch (ProviderCallException e) { models.Add(new(model, e.Status, "provider_http_error")); }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { models.Add(new(model, null, "timeout")); }
            catch (HttpRequestException) { models.Add(new(model, null, "network_error")); }
            catch (JsonException) { models.Add(new(model, 200, "invalid_response")); }
            catch (InvalidDataException) { models.Add(new(model, 200, "invalid_response")); }
            catch (FormatException) { models.Add(new(model, null, "credential_invalid")); }
        }
        return new(DateTime.UtcNow, models, "metadata_only", false, OpenAiHomologationPolicy.CostBoundsVerified);
    }
}

public sealed record OpenAiModelAccess(string Model, int? HttpStatus, string Status);
public sealed record OpenAiModelAccessReport(DateTime CheckedAtUtc, IReadOnlyList<OpenAiModelAccess> Models,
    string Scope, bool ImageEndpointAccessVerified, bool CostBoundsVerified);
