using System.Text.Json;
using System.Text.RegularExpressions;
using Imagino.Api.Security;
using Microsoft.Extensions.Options;

namespace Imagino.Api.Services.Generation;

public sealed class RunwayGenerationProvider(GenerationProviderHttp http, IOptions<GenerationSettings> options) : IGenerationProvider
{
    public const string Origin = "https://api.dev.runwayml.com";
    public const string Endpoint = Origin + "/v1/image_to_video";
    public const string ApiVersion = "2024-11-06";
    public static readonly string[] OutputHosts = { "dnznrvs05pmza.cloudfront.net" };
    public string Name => "runway";
    public bool IsConfigured => options.Value.RunwayIntegrationEnabled && !string.IsNullOrWhiteSpace(options.Value.RunwayApiKey);
    public async Task<ProviderResult> StartAsync(GenerationJob job, CancellationToken ct)
    {
        if (!options.Value.PaidGenerationEnabled || !options.Value.RunwayRealSmokeEnabled || !IsConfigured)
            throw new InvalidOperationException("Runway real smoke is closed.");
        RunwaySmokePolicy.ValidateJob(job);
        if (job.ProviderJobId != null) throw new InvalidOperationException("A bound Runway task cannot be submitted again.");
        var acceptance = System.Diagnostics.Stopwatch.StartNew();
        // Direct HTTP has no retry handler. Never use the SDK's automatic creation retries.
        using var response = await http.SendAsync(HttpMethod.Post, Endpoint, "Authorization", "Bearer " + options.Value.RunwayApiKey,
            new { model = job.Model.ProviderModel, promptText = job.Prompt, duration = 5, ratio = "auto_720p",
                promptImage = new[] { new { uri = job.Inputs.Single().Data, position = "first" } } },
            new[] { "api.dev.runwayml.com" }, ct, apiVersion: ApiVersion);
        var root = response.RootElement;
        var id = root.GetProperty("id").GetString()!;
        ValidateTaskId(id);
        // Preserve a received task ID even when the provider's cost metadata is invalid.
        var estimated = ReadCost(root, "estimatedCost");
        return new(id, Origin + "/v1/tasks/" + id, AcceptanceLatencyMs: acceptance.Elapsed.TotalMilliseconds,
            HttpStatus: 200, ProviderStatus: "PENDING", EstimatedCostUsd: estimated);
    }
    public async Task<ProviderResult> PollAsync(GenerationJob job, CancellationToken ct)
    {
        ValidateTaskId(job.ProviderJobId!);
        var url = Origin + "/v1/tasks/" + job.ProviderJobId;
        if (job.PollingUrl != url) throw new InvalidDataException("Runway polling URL differs from the persisted task binding.");
        using var response = await http.SendAsync(HttpMethod.Get, url, "Authorization", "Bearer " + options.Value.RunwayApiKey,
            null, new[] { "api.dev.runwayml.com" }, ct, apiVersion: ApiVersion);
        var root = response.RootElement;
        if (root.GetProperty("id").GetString() != job.ProviderJobId) throw new InvalidDataException("Foreign Runway task response.");
        var status = root.GetProperty("status").GetString();
        var cost = ReadCost(root, status is "SUCCEEDED" or "FAILED" or "CANCELLED" ? "cost" : "estimatedCost");
        if (status is "PENDING" or "THROTTLED" or "RUNNING")
            return new(job.ProviderJobId, url, ProviderStatus: status, EstimatedCostUsd: cost);
        if (status is "FAILED" or "CANCELLED")
        {
            var moderation = root.TryGetProperty("failureCode", out var failure) && failure.ValueKind == JsonValueKind.String &&
                (failure.GetString()!.StartsWith("SAFETY.") || failure.GetString()!.StartsWith("INPUT_PREPROCESSING.SAFETY."));
            return new(job.ProviderJobId, url, ErrorCode: status == "CANCELLED" ? "provider_cancelled" : moderation ? "provider_moderated" : "provider_failed",
                CostUsd: cost, ProviderStatus: status);
        }
        if (status != "SUCCEEDED") throw new InvalidDataException("Unknown Runway task state.");
        var outputs = root.GetProperty("output");
        if (outputs.ValueKind != JsonValueKind.Array || outputs.GetArrayLength() != 1) throw new InvalidDataException("Expected exactly one Runway output.");
        var output = outputs[0].GetString()!;
        RemoteUrlPolicy.Validate(output, OutputHosts);
        return new(job.ProviderJobId, url, Completed: true, OutputUrl: output, CostUsd: cost, ProviderStatus: status);
    }
    private static decimal? ReadCost(JsonElement root, string key) => root.TryGetProperty(key, out var cost) &&
        cost.ValueKind == JsonValueKind.Object && cost.TryGetProperty("credits", out var credits) &&
        credits.ValueKind == JsonValueKind.Number && credits.TryGetDecimal(out var value) && value >= 0 ? value * 0.01m : null;
    public static void ValidateTaskId(string id)
    {
        if (id == null || !Regex.IsMatch(id, "^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-4[0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}$"))
            throw new InvalidDataException("Invalid Runway task ID.");
    }
}
