using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Imagino.Api.Models.Image;
using Imagino.Api.Services.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;

namespace Imagino.Api.Services.Image.Providers
{
    public class GoogleGeminiImageProviderClient : IImageProviderClient
    {
        public ImageProviderType ProviderType => ImageProviderType.GoogleGemini;

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly IStorageService _storage;
        private readonly ILogger<GoogleGeminiImageProviderClient> _logger;

        public GoogleGeminiImageProviderClient(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IStorageService storage,
            ILogger<GoogleGeminiImageProviderClient> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _storage = storage;
            _logger = logger;
        }

        public async Task<ProviderJobResult> CreateJobAsync(
            ImageModelProvider provider,
            ImageModelVersion version,
            BsonDocument resolvedParams)
        {
            var prompt = resolvedParams.TryGetValue("prompt", out var promptVal) && promptVal.IsString
                ? promptVal.AsString
                : string.Empty;

            var aspectRatio = resolvedParams.TryGetValue("aspect_ratio", out var aspectVal) && aspectVal.IsString
                ? aspectVal.AsString
                : "1:1";

            var imageSize = resolvedParams.TryGetValue("image_size", out var sizeVal) && sizeVal.IsString
                ? sizeVal.AsString
                : "1K";

            var googleSearch = resolvedParams.TryGetValue("google_search", out var searchVal) && searchVal.IsBoolean
                ? searchVal.ToBoolean()
                : false;

            var apiKey = _configuration["GeminiSettings:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("Gemini API key not configured");
            }

            var payload = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[] { new { text = prompt } }
                    }
                },
                tools = googleSearch ? new[] { new { google_search = new { } } } : Array.Empty<object>(),
                generationConfig = new
                {
                    responseModalities = new[] { "IMAGE" },
                    imageConfig = new
                    {
                        aspectRatio,
                        imageSize
                    }
                }
            };

            var endpoint = "https://generativelanguage.googleapis.com/v1beta/models/gemini-3-pro-image-preview:generateContent" +
                           $"?key={apiKey}";

            var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Content = JsonContent.Create(payload, options: new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            string? base64Image = null;

            try
            {
                var parts = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts");

                foreach (var part in parts.EnumerateArray())
                {
                    if (part.TryGetProperty("inlineData", out var inlineData))
                    {
                        base64Image = inlineData.GetProperty("data").GetString();
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse Gemini image response");
            }

            var jobId = ObjectId.GenerateNewId().ToString();
            string? imageUrl = null;

            if (!string.IsNullOrEmpty(base64Image))
            {
                try
                {
                    var data = base64Image;
                    var commaIndex = data.IndexOf(',');
                    if (commaIndex >= 0)
                    {
                        data = data[(commaIndex + 1)..];
                    }

                    var bytes = Convert.FromBase64String(data);
                    await using var ms = new MemoryStream(bytes);
                    var key = $"images/{jobId}.png";
                    imageUrl = await _storage.UploadAsync(ms, key, "image/png");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to upload image for job {JobId}", jobId);
                }
            }

            var status = "completed";
            return new ProviderJobResult(jobId, status, imageUrl);
        }
    }
}
