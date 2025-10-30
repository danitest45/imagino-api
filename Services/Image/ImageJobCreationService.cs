using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Imagino.Api.DTOs;
using Imagino.Api.DTOs.Image;
using Imagino.Api.Errors;
using Imagino.Api.Models;
using Imagino.Api.Models.Image;
using Imagino.Api.Repositories.Image;
using Imagino.Api.Repository;
using MongoDB.Bson;
using MongoDB.Bson.IO;

namespace Imagino.Api.Services.Image
{
    public class ImageJobCreationService : IImageJobCreationService
    {
        private readonly IModelResolverService _modelResolverService;
        private readonly IImageJobRepository _jobRepository;
        private readonly IUserRepository _userRepository;
        private readonly IImageModelProviderRepository _providerRepository;
        private readonly IHttpClientFactory _httpClientFactory;

        public ImageJobCreationService(
            IModelResolverService modelResolverService,
            IImageJobRepository jobRepository,
            IUserRepository userRepository,
            IImageModelProviderRepository providerRepository,
            IHttpClientFactory httpClientFactory)
        {
            _modelResolverService = modelResolverService;
            _jobRepository = jobRepository;
            _userRepository = userRepository;
            _providerRepository = providerRepository;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<JobCreatedResponse> CreateJobAsync(CreateImageJobRequest request, string userId)
        {
            var user = await _userRepository.GetByIdAsync(userId)
                       ?? throw new ValidationAppException("User not found");

            ResolvedPreset? resolvedPreset = null;
            ResolvedModelVersion? resolvedModelVersion = null;

            if (!string.IsNullOrWhiteSpace(request.PresetId))
            {
                resolvedPreset = await _modelResolverService.ResolvePresetAsync(request.PresetId, request.Params);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(request.ModelSlug))
                {
                    throw new ValidationAppException("Model slug must be provided when presetId is not informed");
                }

                resolvedModelVersion = await _modelResolverService.ResolveModelAndVersionAsync(request.ModelSlug, null, request.Params);
            }

            var model = resolvedPreset?.Model ?? resolvedModelVersion!.Model;
            var version = resolvedPreset?.Version ?? resolvedModelVersion!.Version;
            var resolvedParams = resolvedPreset?.ResolvedParams ?? resolvedModelVersion!.ResolvedParams;

            if (resolvedParams.TryGetValue("quality", out var qualityVal) && qualityVal.IsInt32)
            {
                var (steps, guidance) = qualityVal.AsInt32 switch
                {
                    1 => (10, 2.0),
                    2 => (15, 2.5),
                    3 => (25, 3.0),
                    4 => (35, 4.0),
                    5 => (50, 5.0),
                    _ => (25, 3.0)
                };

                resolvedParams["num_inference_steps"] = steps;
                resolvedParams["guidance"] = guidance;
                resolvedParams.Remove("quality");
            }

            var provider = await _providerRepository.GetByIdAsync(model.ProviderId)
                           ?? throw new ValidationAppException($"Provider '{model.ProviderId}' not found");

            var pricing = version.Pricing ?? model.Pricing;
            if (pricing == null || pricing.CreditsPerImage <= 0)
            {
                throw new ValidationAppException("Pricing not configured for the selected model version");
            }

            if (!await _userRepository.DecrementCreditsAsync(userId, pricing.CreditsPerImage))
            {
                throw new InsufficientCreditsException(user.Credits, pricing.CreditsPerImage);
            }

            try
            {
                var (jobId, status) = await DispatchToProviderAsync(provider, version, resolvedParams);

                var prompt = resolvedParams.TryGetValue("prompt", out var promptValue) && promptValue.IsString
                    ? promptValue.AsString
                    : null;

                var aspectRatio = resolvedParams.TryGetValue("aspect_ratio", out var aspectRatioValue) && aspectRatioValue.IsString
                    ? aspectRatioValue.AsString
                    : string.Empty;

                var job = new ImageJob
                {
                    Prompt = prompt,
                    JobId = jobId,
                    Status = status,
                    UserId = userId,
                    ModelSlug = model.Slug,
                    VersionTag = version.VersionTag,
                    PresetId = resolvedPreset?.Preset.Id ?? request.PresetId,
                    ResolvedParams = resolvedParams,
                    AspectRatio = aspectRatio,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    TokenConsumed = true
                };

                await _jobRepository.InsertAsync(job);

                return new JobCreatedResponse
                {
                    JobId = job.JobId,
                    Status = job.Status,
                    CreatedAt = job.CreatedAt,
                    Model = model.Slug,
                    Version = version.VersionTag,
                    PresetId = job.PresetId
                };
            }
            catch
            {
                await _userRepository.IncrementCreditsAsync(userId, pricing.CreditsPerImage);
                throw;
            }
        }

        private async Task<(string JobId, string Status)> DispatchToProviderAsync(ImageModelProvider provider, ImageModelVersion version, BsonDocument resolvedParams)
        {
            var client = _httpClientFactory.CreateClient("ImageModelProvider");
            var url = BuildEndpointUrl(provider, version.EndpointUrl);

            using var message = new HttpRequestMessage(HttpMethod.Post, url);
            ApplyAuth(provider, message);

            var payloadJson = BuildPayload(version, resolvedParams);
            message.Content = new StringContent(payloadJson, Encoding.UTF8, "application/json");

            var response = await client.SendAsync(message);
            if (!response.IsSuccessStatusCode)
            {
                throw new UpstreamServiceException(provider.Name, response.StatusCode.ToString(), (int)response.StatusCode);
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;

            var jobId = root.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String
                ? idProp.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(jobId))
            {
                jobId = ObjectId.GenerateNewId().ToString();
            }

            var status = root.TryGetProperty("status", out var statusProp) && statusProp.ValueKind == JsonValueKind.String
                ? statusProp.GetString() ?? "queued"
                : "queued";

            return (jobId!, status.ToLowerInvariant());
        }

        private static string BuildPayload(ImageModelVersion version, BsonDocument resolvedParams)
        {
            var inputJson = JsonNode.Parse(resolvedParams.ToJson()) ?? new JsonObject();
            var payload = new JsonObject
            {
                ["input"] = inputJson
            };

            if (version.WebhookConfig?.Url != null)
            {
                payload["webhook"] = version.WebhookConfig.Url;
                if (version.WebhookConfig.Events is { Count: > 0 })
                {
                    var eventsArray = new JsonArray();
                    foreach (var evt in version.WebhookConfig.Events)
                    {
                        eventsArray.Add(evt);
                    }

                    payload["webhook_events_filter"] = eventsArray;
                }
            }

            if (version.Rollout?.CanaryPercent is int canary)
            {
                payload["canary_percent"] = canary;
            }

            return payload.ToJsonString(new JsonSerializerOptions
            {
                PropertyNamingPolicy = null,
                WriteIndented = false
            });
        }

        private static string BuildEndpointUrl(ImageModelProvider provider, string endpointUrl)
        {
            if (Uri.TryCreate(endpointUrl, UriKind.Absolute, out var absolute))
            {
                return absolute.ToString();
            }

            if (string.IsNullOrWhiteSpace(provider.Auth.BaseUrl))
            {
                throw new ValidationAppException($"Provider '{provider.Name}' does not have a base URL configured");
            }

            return new Uri(new Uri(provider.Auth.BaseUrl!, UriKind.Absolute), endpointUrl).ToString();
        }

        private static void ApplyAuth(ImageModelProvider provider, HttpRequestMessage message)
        {
            var auth = provider.Auth;
            var headerName = string.IsNullOrWhiteSpace(auth.Header) ? "Authorization" : auth.Header;

            switch (auth.Mode)
            {
                case ImageModelProviderAuthMode.SecretRef:
                    if (string.IsNullOrWhiteSpace(auth.SecretRef))
                    {
                        throw new ValidationAppException($"Provider '{provider.Name}' is missing SecretRef");
                    }

                    var tokenValue = string.IsNullOrWhiteSpace(auth.Scheme)
                        ? auth.SecretRef
                        : $"{auth.Scheme} {auth.SecretRef}";

                    message.Headers.Remove(headerName);
                    message.Headers.TryAddWithoutValidation(headerName, tokenValue);
                    break;
                case ImageModelProviderAuthMode.Encrypted:
                    throw new ValidationAppException($"Provider '{provider.Name}' uses unsupported encrypted authentication mode");
                default:
                    throw new ValidationAppException($"Provider '{provider.Name}' has an unknown authentication mode");
            }
        }
    }
}
