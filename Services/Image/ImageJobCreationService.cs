using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Imagino.Api.DTOs;
using Imagino.Api.DTOs.Image;
using Imagino.Api.Errors;
using Imagino.Api.Models;
using Imagino.Api.Models.Image;
using Imagino.Api.Repositories.Image;
using Imagino.Api.Repository;
using Microsoft.Extensions.Configuration;
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
        private readonly IConfiguration _configuration;

        public ImageJobCreationService(
            IModelResolverService modelResolverService,
            IImageJobRepository jobRepository,
            IUserRepository userRepository,
            IImageModelProviderRepository providerRepository,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _modelResolverService = modelResolverService;
            _jobRepository = jobRepository;
            _userRepository = userRepository;
            _providerRepository = providerRepository;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
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

        private string BuildPayload(ImageModelVersion version, BsonDocument resolvedParams)
        {
            var payload = new BsonDocument
            {
                ["input"] = resolvedParams.DeepClone()
            };

            if (!string.IsNullOrWhiteSpace(version.WebhookConfig?.Url))
            {
                var resolvedWebhookUrl = ResolveWebhookUrl(version.WebhookConfig.Url);
                payload["webhook"] = resolvedWebhookUrl;

                var events = ResolveWebhookEvents(version.WebhookConfig.Events);
                if (events.Count > 0)
                {
                    payload["webhook_events_filter"] = new BsonArray(events);
                }
            }

            if (version.Rollout?.CanaryPercent is int canary)
            {
                payload["canary_percent"] = canary;
            }

            return payload.ToJson(RelaxedJsonWriterSettings);
        }

        private static readonly JsonWriterSettings RelaxedJsonWriterSettings = new()
        {
            OutputMode = JsonOutputMode.RelaxedExtendedJson
        };

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

        private void ApplyAuth(ImageModelProvider provider, HttpRequestMessage message)
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

                    // 1️⃣ Tenta pela variável de ambiente
                    var secretValue = Environment.GetEnvironmentVariable(auth.SecretRef);

                    // 2️⃣ Se não achar, tenta pelo appsettings (IConfiguration)
                    if (string.IsNullOrWhiteSpace(secretValue))
                    {
                        var configValue = _configuration[auth.SecretRef];
                        secretValue = configValue;
                    }

                    if (string.IsNullOrWhiteSpace(secretValue))
                    {
                        throw new ValidationAppException($"Configuration or environment variable '{auth.SecretRef}' is not configured");
                    }

                    var tokenValue = string.IsNullOrWhiteSpace(auth.Scheme)
                        ? secretValue
                        : $"{auth.Scheme} {secretValue}";

                    message.Headers.Remove(headerName);
                    message.Headers.TryAddWithoutValidation(headerName, tokenValue);
                    break;
                case ImageModelProviderAuthMode.Encrypted:
                    throw new ValidationAppException($"Provider '{provider.Name}' uses unsupported encrypted authentication mode");
                default:
                    throw new ValidationAppException($"Provider '{provider.Name}' has an unknown authentication mode");
            }
        }

        private static readonly IReadOnlyList<string> DefaultWebhookEvents = new[] { "completed" };

        private string ResolveWebhookUrl(string configuredValue)
        {
            if (Uri.TryCreate(configuredValue, UriKind.Absolute, out var directUri))
            {
                return directUri.ToString();
            }

            var resolvedValue = TryResolveConfigurationValue(configuredValue);

            if (string.IsNullOrWhiteSpace(resolvedValue))
            {
                throw new ValidationAppException($"Webhook URL configuration '{configuredValue}' is not configured");
            }

            if (!Uri.TryCreate(resolvedValue, UriKind.Absolute, out var resolvedUri))
            {
                throw new ValidationAppException($"Resolved webhook URL for '{configuredValue}' is invalid");
            }

            return resolvedUri.ToString();
        }

        private static IReadOnlyList<string> ResolveWebhookEvents(List<string>? configuredEvents)
        {
            if (configuredEvents is { Count: > 0 })
            {
                return configuredEvents;
            }

            return DefaultWebhookEvents;
        }

        private string? TryResolveConfigurationValue(string key)
        {
            var candidates = BuildEnvironmentKeyCandidates(key).ToArray();
            foreach (var candidate in candidates)
            {
                var envValue = Environment.GetEnvironmentVariable(candidate);
                if (!string.IsNullOrWhiteSpace(envValue))
                {
                    return envValue;
                }
            }

            var environmentVariables = Environment.GetEnvironmentVariables();
            foreach (DictionaryEntry entry in environmentVariables)
            {
                if (entry.Key is string envKey && candidates.Any(candidate => string.Equals(envKey, candidate, StringComparison.OrdinalIgnoreCase)))
                {
                    var value = entry.Value?.ToString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
            }

            var configValue = _configuration.GetValue<string>(key);
            if (!string.IsNullOrWhiteSpace(configValue))
            {
                return configValue;
            }

            configValue = _configuration[key];
            if (!string.IsNullOrWhiteSpace(configValue))
            {
                return configValue;
            }

            if (key.Contains(':'))
            {
                var normalizedKey = key.Replace(":", "__");
                configValue = _configuration.GetValue<string>(normalizedKey);
                if (!string.IsNullOrWhiteSpace(configValue))
                {
                    return configValue;
                }

                configValue = _configuration[normalizedKey];
                if (!string.IsNullOrWhiteSpace(configValue))
                {
                    return configValue;
                }
            }

            var section = _configuration.GetSection(key);
            if (section.Exists())
            {
                var sectionValue = section.Value;
                if (!string.IsNullOrWhiteSpace(sectionValue))
                {
                    return sectionValue;
                }

                sectionValue = section.Get<string?>();
                if (!string.IsNullOrWhiteSpace(sectionValue))
                {
                    return sectionValue;
                }
            }

            if (key.Contains(':'))
            {
                var normalizedKey = key.Replace(":", "__");
                var normalizedSection = _configuration.GetSection(normalizedKey);
                if (normalizedSection.Exists())
                {
                    var normalizedValue = normalizedSection.Value;
                    if (!string.IsNullOrWhiteSpace(normalizedValue))
                    {
                        return normalizedValue;
                    }

                    normalizedValue = normalizedSection.Get<string?>();
                    if (!string.IsNullOrWhiteSpace(normalizedValue))
                    {
                        return normalizedValue;
                    }
                }
            }

            return null;
        }

        private static IEnumerable<string> BuildEnvironmentKeyCandidates(string key)
        {
            yield return key;

            if (key.Contains(':'))
            {
                yield return key.Replace(":", "__");
            }

            var upperKey = key.ToUpperInvariant();
            if (!string.Equals(upperKey, key, StringComparison.Ordinal))
            {
                yield return upperKey;
            }

            if (key.Contains(':'))
            {
                var underscoreUpper = key.Replace(":", "__").ToUpperInvariant();
                if (!string.Equals(underscoreUpper, upperKey, StringComparison.Ordinal))
                {
                    yield return underscoreUpper;
                }
            }
        }
    }
}
