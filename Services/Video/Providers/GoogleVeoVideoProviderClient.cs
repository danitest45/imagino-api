using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
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
        private readonly IStorageService _storage;
        private readonly ILogger<GoogleVeoVideoProviderClient> _logger;

        public GoogleVeoVideoProviderClient(
            IHttpClientFactory httpClientFactory,
            IOptions<VeoSettings> settings,
            IStorageService storage,
            ILogger<GoogleVeoVideoProviderClient> logger)
        {
            _httpClientFactory = httpClientFactory;
            _settings = settings.Value;
            _storage = storage;
            _logger = logger;
        }

        public async Task<VideoProviderJobResult> CreateJobAsync(VideoModelProvider provider, VideoModelVersion version, BsonDocument resolvedParams)
        {
            var prompt = resolvedParams.TryGetValue("prompt", out var promptVal) && promptVal.IsString
                ? promptVal.AsString
                : string.Empty;

            if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            {
                throw new InvalidOperationException("Veo API key not configured");
            }

            var parameters = BuildParameters(resolvedParams);

            var payload = new
            {
                instances = new[]
                {
                    new
                    {
                        prompt
                    }
                },
                parameters = parameters
            };

            var endpoint = "https://generativelanguage.googleapis.com/v1beta/models/veo-3.1-generate-preview:predictLongRunning";

            var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = JsonContent.Create(payload, options: new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                })
            };

            request.Headers.Add("x-goog-api-key", _settings.ApiKey);

            var response = await client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Google Veo API error: {response.StatusCode} - {content}");
            }

            using var doc = JsonDocument.Parse(content);
            var operationName = doc.RootElement.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String
                ? nameElement.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(operationName))
            {
                throw new Exception($"Google Veo API error: missing operation name - {content}");
            }

            return new VideoProviderJobResult(operationName!, VideoJobStatus.Running, null);
        }

        public async Task<VideoProviderJobResult> PollResultAsync(VideoModelProvider provider, VideoModelVersion version, string providerJobId, BsonDocument resolvedParams, string? outputFileName = null)
        {
            if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            {
                throw new InvalidOperationException("Veo API key not configured");
            }

            var endpoint = $"https://generativelanguage.googleapis.com/v1beta/{providerJobId}";
            var client = _httpClientFactory.CreateClient();

            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.Add("x-goog-api-key", _settings.ApiKey);

            var response = await client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Google Veo API error: {response.StatusCode} - {content}");
            }

            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            if (root.TryGetProperty("error", out _))
            {
                throw new Exception($"Google Veo API error: {content}");
            }

            if (root.TryGetProperty("done", out var doneElement) && doneElement.ValueKind == JsonValueKind.True)
            {
                var videoUrl = ExtractVideoUri(root);
                if (string.IsNullOrWhiteSpace(videoUrl))
                {
                    throw new Exception($"Google Veo API error: missing video uri - {content}");
                }

                var fileName = string.IsNullOrWhiteSpace(outputFileName) ? providerJobId : outputFileName;
                var storedUrl = await DownloadAndStoreVideoAsync(videoUrl, fileName!, CancellationToken.None);

                return new VideoProviderJobResult(providerJobId, VideoJobStatus.Completed, storedUrl);
            }

            return new VideoProviderJobResult(providerJobId, VideoJobStatus.Running, null);
        }

        private async Task<string> DownloadAndStoreVideoAsync(string videoUri, string fileName, CancellationToken cancellationToken)
        {
            var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, videoUri);
            request.Headers.Add("x-goog-api-key", _settings.ApiKey);

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to download video from Veo. Status: {Status}. Body: {Body}", response.StatusCode, errorBody);
                throw new Exception($"Failed to download video from Veo: {response.StatusCode} - {errorBody}");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var normalizedFileName = fileName.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
                ? fileName
                : $"{fileName}.mp4";

            try
            {
                return await _storage.UploadVideoAsync(stream, normalizedFileName, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload video {FileName} to R2", normalizedFileName);
                throw new Exception("Failed to store generated video");
            }
        }

        private static string? ExtractVideoUri(JsonElement root)
        {
            if (root.TryGetProperty("response", out var responseElement)
                && responseElement.TryGetProperty("generateVideoResponse", out var videoResponse)
                && videoResponse.TryGetProperty("generatedSamples", out var samples)
                && samples.ValueKind == JsonValueKind.Array)
            {
                var first = samples.EnumerateArray().FirstOrDefault();
                if (first.ValueKind == JsonValueKind.Object
                    && first.TryGetProperty("video", out var video)
                    && video.TryGetProperty("uri", out var uriElement)
                    && uriElement.ValueKind == JsonValueKind.String)
                {
                    return uriElement.GetString();
                }
            }

            return null;
        }

        private static object? BuildParameters(BsonDocument resolvedParams)
        {
            var parameters = new Dictionary<string, object>();

            if (resolvedParams.TryGetValue("aspect_ratio", out var aspectRatioVal) && aspectRatioVal.IsString)
            {
                parameters["aspectRatio"] = aspectRatioVal.AsString;
            }

            if (resolvedParams.TryGetValue("negative_prompt", out var negativePromptVal) && negativePromptVal.IsString)
            {
                parameters["negativePrompt"] = negativePromptVal.AsString;
            }

            var durationSeconds = ResolveDurationSeconds(resolvedParams);
            if (durationSeconds > 0)
            {
                parameters["durationSeconds"] = durationSeconds;
            }

            if (resolvedParams.TryGetValue("resolution", out var resolutionVal) && resolutionVal.IsString)
            {
                parameters["resolution"] = resolutionVal.AsString;
            }

            return parameters.Count > 0 ? parameters : null;
        }

        private static int ResolveDurationSeconds(BsonDocument resolvedParams)
        {
            if (resolvedParams.TryGetValue("max_output_length_seconds", out var durationVal) && durationVal.IsNumeric)
            {
                return Convert.ToInt32(Math.Ceiling(durationVal.ToDouble()));
            }

            if (resolvedParams.TryGetValue("duration", out var altDuration) && altDuration.IsNumeric)
            {
                return Convert.ToInt32(Math.Ceiling(altDuration.ToDouble()));
            }

            return 0;
        }
    }
}
