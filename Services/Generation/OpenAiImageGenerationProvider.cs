using System.Diagnostics;
using System.Text.Json;
using Imagino.Api.Security;
using Microsoft.Extensions.Options;

namespace Imagino.Api.Services.Generation;

// The direct Images API is synchronous. No provider job ID or polling URL is invented.
public sealed class OpenAiImageGenerationProvider(GenerationProviderHttp http, IOptions<GenerationSettings> options) : IGenerationProvider
{
    public string Name => "openai";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.OpenAiApiKey);
    public async Task<ProviderResult> StartAsync(GenerationJob job, CancellationToken ct)
    {
        if (job.OpenAiRunId == OpenAiSingleSmokePolicy.RunId)
        {
            if (!options.Value.OpenAiSingleSmokeEnabled || !options.Value.PaidGenerationEnabled || !options.Value.OpenAiHomologationEnabled)
                throw new Imagino.Api.Errors.ForbiddenFeatureException("Single Flare authorization is closed.");
            OpenAiSingleSmokePolicy.ValidateJob(job);
        }
        else OpenAiHomologationPolicy.ValidateJob(job);
        var body = new Dictionary<string, object> {
            ["model"] = job.Model.ProviderModel, ["prompt"] = job.Prompt,
            ["size"] = "1024x1024", ["quality"] = "medium", ["n"] = 1,
            ["output_format"] = "png", ["background"] = "opaque", ["stream"] = false
        };
        var endpoint = job.Inputs.Count == 0 ? "generations" : "edits";
        if (endpoint == "edits") body["images"] = job.Inputs.Select(i => new { image_url = i.Data }).ToArray();
        var timer = Stopwatch.StartNew();
        int? observedStatus = null;
        using var response = await http.SendAsync(HttpMethod.Post, "https://api.openai.com/v1/images/" + endpoint,
            "Authorization", "Bearer " + options.Value.OpenAiApiKey, body, new[] { "api.openai.com" }, ct, status => observedStatus = status);
        var postMs = timer.Elapsed.TotalMilliseconds;
        timer.Restart();
        var parsed = ParseResponse(response.RootElement);
        return parsed with { AcceptanceLatencyMs = postMs, DecodeLatencyMs = timer.Elapsed.TotalMilliseconds, HttpStatus = observedStatus };
    }
    public Task<ProviderResult> PollAsync(GenerationJob job, CancellationToken ct) =>
        throw new InvalidOperationException("The direct Images API has no polling contract.");

    public static ProviderResult ParseResponse(JsonElement root)
    {
        // Only numeric usage and the single decoded image leave this method. No raw provider response.
        if (!root.TryGetProperty("usage", out var usage)) throw new InvalidDataException("Missing image usage.");
        var input = Count(usage, "input_tokens"); var output = Count(usage, "output_tokens"); var total = Count(usage, "total_tokens");
        if (!usage.TryGetProperty("input_tokens_details", out var details)) throw new InvalidDataException("Missing input usage details.");
        var text = Count(details, "text_tokens"); var image = Count(details, "image_tokens");
        var cached = details.TryGetProperty("cached_tokens", out _) ? Count(details, "cached_tokens") : 0;
        var imageOutput = output;
        if (usage.TryGetProperty("output_tokens_details", out var outputDetails))
        {
            imageOutput = Count(outputDetails, "image_tokens");
            if (outputDetails.TryGetProperty("text_tokens", out _) && Count(outputDetails, "text_tokens") != 0)
                throw new InvalidDataException("Unpriced output token modality.");
        }
        if (text + image != input || input + output != total || imageOutput != output || cached > input || output == 0)
            throw new InvalidDataException("Inconsistent image usage.");
        var measured = new GenerationUsage(text, image, imageOutput, input, output, total, cached, OpenAiImagePricing.Revision);
        var cost = OpenAiImagePricing.Calculate(measured);
        ProviderResult InvalidOutput() => new(null, null, Completed: true, ErrorCode: "invalid_provider_output", CostUsd: cost, Usage: measured);
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array || data.GetArrayLength() != 1 ||
            !data[0].TryGetProperty("b64_json", out var encoded) || encoded.ValueKind != JsonValueKind.String)
            return InvalidOutput();
        var value = encoded.GetString()!;
        if (value.Length == 0 || value.Length > GeneratedImageValidator.MaxBase64Chars)
            return InvalidOutput();
        byte[] bytes;
        try { bytes = Convert.FromBase64String(value); }
        catch (FormatException) { return InvalidOutput(); }
        // Usage survives even an invalid decoded output; validation/storage belong to the worker.
        return new(null, null, Completed: true, Bytes: bytes, CostUsd: cost, Usage: measured);
    }
    private static long Count(JsonElement element, string key)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(key, out var value) ||
            value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var count) || count is < 0 or > 1_000_000_000)
            throw new InvalidDataException("Invalid numeric image usage.");
        return count;
    }
}

public static class OpenAiImagePricing
{
    public const string Revision = "openai-standard-2026-10-06.1-experimental";
    public const long OutputOnlyProjectedTokens = 439;
    public const decimal TextInputPerMillion = 5m, ImageInputPerMillion = 8m, ImageOutputPerMillion = 30m;
    public static decimal Calculate(GenerationUsage usage)
    {
        if (usage.PricingRevision != Revision || usage.TextInputTokens < 0 || usage.ImageInputTokens < 0 || usage.ImageOutputTokens < 0)
            throw new InvalidDataException("Unrecognized image pricing usage.");
        // Cached discounts are not applicable to the direct Images API, including edits.
        return (usage.TextInputTokens * TextInputPerMillion + usage.ImageInputTokens * ImageInputPerMillion +
            usage.ImageOutputTokens * ImageOutputPerMillion) / 1_000_000m;
    }
    public static int ExperimentalCredits(decimal observedUsd, GenerationPricing planning)
    {
        if (observedUsd < 0 || planning.RiskMultiplier < 1 || planning.OverheadUsd < 0 ||
            planning.TargetMargin is < 0 or >= 1 || planning.CreditValueUsd <= 0) throw new InvalidDataException("Invalid experimental planning policy.");
        return Math.Max(1, checked((int)decimal.Ceiling((observedUsd * planning.RiskMultiplier + planning.OverheadUsd) /
            ((1 - planning.TargetMargin) * planning.CreditValueUsd))));
    }
}
