using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace Imagino.Api.Services.Generation;

public sealed class BflGenerationProvider(GenerationProviderHttp http, IOptions<GenerationSettings> options) : IGenerationProvider
{
    public string Name => "bfl";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.BflApiKey);
    public async Task<ProviderResult> StartAsync(GenerationJob job, CancellationToken ct)
    {
        if (job.Model.ProviderModel is not ("flux-2-klein-4b" or "flux-2-pro")) throw new InvalidOperationException("Unsupported BFL endpoint.");
        if (options.Value.BflHomologationEnabled) BflHomologationPolicy.ValidateJob(job);
        var (width, height) = GenerationPolicy.Dimensions(job.Settings);
        var body = new Dictionary<string, object> { ["prompt"] = job.Prompt, ["width"] = width, ["height"] = height, ["output_format"] = "png", ["safety_tolerance"] = 2 };
        if (options.Value.BflHomologationEnabled)
        {
            body["seed"] = 20261005;
            if (job.Model.ProviderModel == "flux-2-pro") body["disable_pup"] = true;
        }
        for (var i = 0; i < job.Inputs.Count; i++) body[i == 0 ? "input_image" : "input_image_" + (i + 1)] = job.Inputs[i].Data.Split(',')[1];
        var acceptance = System.Diagnostics.Stopwatch.StartNew();
        using var response = await http.SendAsync(HttpMethod.Post, "https://api.bfl.ai/v1/" + job.Model.ProviderModel,
            "x-key", options.Value.BflApiKey, body, new[] { "api.bfl.ai" }, ct);
        var root = response.RootElement;
        var id = root.GetProperty("id").GetString()!;
        var pollingUrl = root.GetProperty("polling_url").GetString()!;
        ValidatePolling(pollingUrl, id);
        // BFL reports cost in provider credits (1 credit = $0.01), not Imagino credits.
        // The live API can serialize 1.4 credits as 1.4000000000000001. Remove only
        // sub-picodollar floating-point noise; meaningful price changes still halt the gate.
        decimal? cost = root.TryGetProperty("cost", out var c) && c.ValueKind == System.Text.Json.JsonValueKind.Number && c.TryGetDecimal(out var n)
            ? decimal.Round(n * 0.01m, 12, MidpointRounding.AwayFromZero) : null;
        return new(id, pollingUrl, CostUsd: cost, AcceptanceLatencyMs: acceptance.Elapsed.TotalMilliseconds);
    }
    public async Task<ProviderResult> PollAsync(GenerationJob job, CancellationToken ct)
    {
        ValidatePolling(job.PollingUrl!, job.ProviderJobId!);
        using var response = await http.SendAsync(HttpMethod.Get, job.PollingUrl!, "x-key", options.Value.BflApiKey,
            null, new[] { "*.bfl.ai" }, ct);
        var root = response.RootElement;
        var status = root.GetProperty("status").GetString();
        return status switch {
            "Ready" => new(job.ProviderJobId, job.PollingUrl, Completed: true, OutputUrl: root.GetProperty("result").GetProperty("sample").GetString()),
            "Error" or "Failed" or "Request Moderated" or "Content Moderated" => new(job.ProviderJobId, job.PollingUrl, ErrorCode: "provider_failed"),
            "Pending" or "Processing" => new(job.ProviderJobId, job.PollingUrl),
            _ => throw new InvalidDataException("Unknown BFL job state.")
        };
    }
    public static void ValidatePolling(string url, string id)
    {
        var uri = Imagino.Api.Security.RemoteUrlPolicy.Validate(url, "*.bfl.ai");
        if (string.IsNullOrWhiteSpace(id) || !Regex.IsMatch(id, "^[A-Za-z0-9_-]{1,100}$") ||
            uri.AbsolutePath != "/v1/get_result" ||
            uri.Query.TrimStart('?').Split('&').Count(p => p.StartsWith("id=", StringComparison.Ordinal)) != 1 ||
            !uri.Query.TrimStart('?').Split('&').Contains("id=" + Uri.EscapeDataString(id)))
            throw new InvalidDataException("Polling URL does not match the bound provider job.");
    }
}
