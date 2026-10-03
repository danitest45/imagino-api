using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace Imagino.Api.Services.Generation;

public sealed class VeoGenerationProvider(GenerationProviderHttp http, IOptions<GenerationSettings> options) : IGenerationProvider
{
    public string Name => "google-veo";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.GeminiApiKey);
    public async Task<ProviderResult> StartAsync(GenerationJob job, CancellationToken ct)
    {
        if (job.Model.ProviderModel is not ("veo-3.1-lite-generate-preview" or "veo-3.1-generate-preview")) throw new InvalidOperationException("Unsupported Veo endpoint.");
        var instance = new Dictionary<string, object> { ["prompt"] = job.Prompt };
        foreach (var image in job.Inputs)
            instance[image.Role == "firstFrame" ? "image" : "lastFrame"] = new { inlineData = new { mimeType = "image/png", data = image.Data.Split(',')[1] } };
        using var response = await http.SendAsync(HttpMethod.Post,
            "https://generativelanguage.googleapis.com/v1beta/models/" + job.Model.ProviderModel + ":predictLongRunning",
            "x-goog-api-key", options.Value.GeminiApiKey,
            new { instances = new[] { instance }, parameters = new { aspectRatio = job.Settings["aspectRatio"],
                resolution = job.Settings["resolution"], durationSeconds = int.Parse(job.Settings["duration"]),
                personGeneration = job.Inputs.Count > 0 ? "allow_adult" : "allow_all" } }, new[] { "generativelanguage.googleapis.com" }, ct);
        var name = response.RootElement.GetProperty("name").GetString()!;
        ValidateName(name, job.Model.ProviderModel);
        return new(name, null);
    }
    public async Task<ProviderResult> PollAsync(GenerationJob job, CancellationToken ct)
    {
        ValidateName(job.ProviderJobId!, job.Model.ProviderModel);
        using var response = await http.SendAsync(HttpMethod.Get, "https://generativelanguage.googleapis.com/v1beta/" + job.ProviderJobId,
            "x-goog-api-key", options.Value.GeminiApiKey, null, new[] { "generativelanguage.googleapis.com" }, ct);
        var root = response.RootElement;
        if (root.TryGetProperty("name", out var name) && name.GetString() != job.ProviderJobId) throw new InvalidDataException("Operation binding mismatch.");
        if (root.TryGetProperty("error", out _)) return new(job.ProviderJobId, null, ErrorCode: "provider_failed");
        if (!root.TryGetProperty("done", out var done) || !done.GetBoolean()) return new(job.ProviderJobId, null);
        var generated = root.GetProperty("response").GetProperty("generateVideoResponse");
        if (!generated.TryGetProperty("generatedSamples", out var samples) || samples.GetArrayLength() == 0)
            return new(job.ProviderJobId, null, ErrorCode: "provider_no_output");
        return new(job.ProviderJobId, null, Completed: true, OutputUrl: samples[0].GetProperty("video").GetProperty("uri").GetString());
    }
    public static void ValidateName(string name, string model)
    {
        if (name == null || !Regex.IsMatch(name, "^models/" + Regex.Escape(model) + "/operations/[A-Za-z0-9_-]{1,200}$"))
            throw new InvalidDataException("Invalid Veo operation binding.");
    }
}
