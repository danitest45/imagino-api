using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace Imagino.Api.Services.Generation;

public sealed class GeminiImageGenerationProvider(GenerationProviderHttp http, IOptions<GenerationSettings> options) : IGenerationProvider
{
    public string Name => "gemini-image";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.GeminiApiKey);
    public async Task<ProviderResult> StartAsync(GenerationJob job, CancellationToken ct)
    {
        if (job.Model.ProviderModel != "gemini-3.1-flash-image") throw new InvalidOperationException("Unsupported Gemini image model.");
        var input = new List<object> { new { type = "text", text = job.Prompt } };
        input.AddRange(job.Inputs.Select(i => (object)new { type = "image", mime_type = "image/png", data = i.Data.Split(',')[1] }));
        using var response = await http.SendAsync(HttpMethod.Post, "https://generativelanguage.googleapis.com/v1beta/interactions",
            "x-goog-api-key", options.Value.GeminiApiKey, new { model = job.Model.ProviderModel, input,
                store = true, response_format = new { type = "image", mime_type = "image/png", aspect_ratio = job.Settings["aspectRatio"], image_size = job.Settings["resolution"] } },
            new[] { "generativelanguage.googleapis.com" }, ct);
        var id = response.RootElement.GetProperty("id").GetString()!;
        ValidateId(id);
        // Bind the stored interaction first; a worker restart can GET it without another paid POST.
        return new(id, "https://generativelanguage.googleapis.com/v1beta/interactions/" + id);
    }
    public async Task<ProviderResult> PollAsync(GenerationJob job, CancellationToken ct)
    {
        ValidateId(job.ProviderJobId!);
        using var response = await http.SendAsync(HttpMethod.Get,
            "https://generativelanguage.googleapis.com/v1beta/interactions/" + job.ProviderJobId,
            "x-goog-api-key", options.Value.GeminiApiKey, null, new[] { "generativelanguage.googleapis.com" }, ct);
        var root = response.RootElement;
        if (root.GetProperty("id").GetString() != job.ProviderJobId) throw new InvalidDataException("Interaction binding mismatch.");
        var status = root.GetProperty("status").GetString();
        if (status is "failed" or "cancelled") return new(job.ProviderJobId, null, ErrorCode: "provider_failed");
        if (status != "completed") return new(job.ProviderJobId, null);
        foreach (var step in root.GetProperty("steps").EnumerateArray())
            if (step.GetProperty("type").GetString() == "model_output")
                foreach (var content in step.GetProperty("content").EnumerateArray())
                    if (content.GetProperty("type").GetString() == "image" && content.TryGetProperty("data", out var data))
                    {
                        var encoded = data.GetString()!;
                        if (encoded.Length > Imagino.Api.Security.GeneratedImageValidator.MaxBase64Chars) throw new InvalidDataException("Image exceeds limit.");
                        return new(job.ProviderJobId, null, Completed: true, Bytes: Convert.FromBase64String(encoded));
                    }
        return new(job.ProviderJobId, null, ErrorCode: "provider_no_output");
    }
    private static void ValidateId(string id)
    {
        if (id == null || !Regex.IsMatch(id, "^[A-Za-z0-9_-]{1,512}$")) throw new InvalidDataException("Invalid interaction id.");
    }
}
