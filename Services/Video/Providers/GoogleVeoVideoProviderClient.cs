using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Imagino.Api.Errors;
using Imagino.Api.Models;
using Imagino.Api.Models.Video;
using Imagino.Api.Services.Storage;
using Imagino.Api.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;

namespace Imagino.Api.Services.Video.Providers
{
    public class GoogleVeoVideoProviderClient : IVideoProviderClient
    {
        public VideoProviderType ProviderType => VideoProviderType.GoogleVeo;

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly VeoSettings _settings;
        private readonly IStorageService _storageService;
        private readonly ILogger<GoogleVeoVideoProviderClient> _logger;

        public GoogleVeoVideoProviderClient(
            IHttpClientFactory httpClientFactory,
            IOptions<VeoSettings> settings,
            IStorageService storageService,
            ILogger<GoogleVeoVideoProviderClient> logger)
        {
            _httpClientFactory = httpClientFactory;
            _settings = settings.Value;
            _storageService = storageService;
            _logger = logger;
        }

        public async Task<VideoProviderJobResult> CreateJobAsync(VideoModelProvider provider, VideoModelVersion version, BsonDocument resolvedParams)
        {
            var prompt = resolvedParams.TryGetValue("prompt", out var promptVal) && promptVal.IsString
                ? promptVal.AsString
                : string.Empty;

            var durationSeconds = resolvedParams.TryGetValue("max_output_length_seconds", out var durationVal) && durationVal.IsNumeric
                ? Convert.ToInt32(durationVal.ToDouble())
                : 0;

            if (durationSeconds <= 0 && resolvedParams.TryGetValue("duration", out var altDuration) && altDuration.IsNumeric)
            {
                durationSeconds = Convert.ToInt32(altDuration.ToDouble());
            }

            if (durationSeconds <= 0)
            {
                durationSeconds = 10;
            }

            var resolution = resolvedParams.TryGetValue("resolution", out var resolutionVal) && resolutionVal.IsString
                ? resolutionVal.AsString
                : "720p";

            if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            {
                throw new InvalidOperationException("Veo API key not configured");
            }

            var payload = new
            {
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[] { new { text = prompt } }
                    }
                },
                generationConfig = new
                {
                    responseModalities = new[] { "VIDEO" },
                    videoConfig = new
                    {
                        max_output_length_seconds = durationSeconds,
                        resolution
                    }
                }
            };

            var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/veo-3.1-001:generateContent?key={_settings.ApiKey}";

            var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = JsonContent.Create(payload, options: new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                })
            };

            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var jobId = ObjectId.GenerateNewId().ToString();
            var videoUrl = ExtractVideoUrl(doc.RootElement);

            if (string.IsNullOrEmpty(videoUrl))
            {
                var base64Video = ExtractBase64Video(doc.RootElement);
                if (!string.IsNullOrEmpty(base64Video))
                {
                    try
                    {
                        var data = base64Video;
                        var commaIndex = data.IndexOf(',');
                        if (commaIndex >= 0)
                        {
                            data = data[(commaIndex + 1)..];
                        }

                        var bytes = Convert.FromBase64String(data);
                        await using var ms = new MemoryStream(bytes);
                        var key = $"videos/{jobId}.mp4";
                        videoUrl = await _storageService.UploadAsync(ms, key, "video/mp4");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to upload video for job {JobId}", jobId);
                        throw new UpstreamServiceException("Google Veo", message: "Failed to upload generated video");
                    }
                }
            }

            if (string.IsNullOrEmpty(videoUrl))
            {
                throw new UpstreamServiceException("Google Veo", message: "Video generation failed");
            }

            return new VideoProviderJobResult(jobId, VideoJobStatus.Completed, videoUrl);
        }

        private static string? ExtractVideoUrl(JsonElement root)
        {
            if (root.TryGetProperty("videoUri", out var directUri) && directUri.ValueKind == JsonValueKind.String)
            {
                return directUri.GetString();
            }

            if (root.TryGetProperty("videoUrl", out var urlElement) && urlElement.ValueKind == JsonValueKind.String)
            {
                return urlElement.GetString();
            }

            if (root.TryGetProperty("videos", out var videos) && videos.ValueKind == JsonValueKind.Array)
            {
                var first = videos.EnumerateArray().FirstOrDefault();
                if (first.ValueKind == JsonValueKind.Object)
                {
                    if (first.TryGetProperty("videoUri", out var videoUri) && videoUri.ValueKind == JsonValueKind.String)
                    {
                        return videoUri.GetString();
                    }

                    if (first.TryGetProperty("videoUrl", out var nestedUrl) && nestedUrl.ValueKind == JsonValueKind.String)
                    {
                        return nestedUrl.GetString();
                    }
                }
            }

            if (root.TryGetProperty("candidates", out var candidates) && candidates.ValueKind == JsonValueKind.Array)
            {
                foreach (var candidate in candidates.EnumerateArray())
                {
                    if (candidate.TryGetProperty("content", out var content) && content.TryGetProperty("parts", out var parts))
                    {
                        foreach (var part in parts.EnumerateArray())
                        {
                            if (part.TryGetProperty("fileData", out var fileData) && fileData.TryGetProperty("fileUri", out var fileUri) && fileUri.ValueKind == JsonValueKind.String)
                            {
                                return fileUri.GetString();
                            }
                        }
                    }
                }
            }

            return null;
        }

        private static string? ExtractBase64Video(JsonElement root)
        {
            if (root.TryGetProperty("candidates", out var candidates) && candidates.ValueKind == JsonValueKind.Array)
            {
                foreach (var candidate in candidates.EnumerateArray())
                {
                    if (candidate.TryGetProperty("content", out var content) && content.TryGetProperty("parts", out var parts))
                    {
                        foreach (var part in parts.EnumerateArray())
                        {
                            if (part.TryGetProperty("inlineData", out var inlineData))
                            {
                                if (inlineData.TryGetProperty("data", out var dataElement) && dataElement.ValueKind == JsonValueKind.String)
                                {
                                    return dataElement.GetString();
                                }
                            }
                        }
                    }
                }
            }

            return null;
        }
    }
}
