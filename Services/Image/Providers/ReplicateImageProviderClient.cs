using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using Imagino.Api.Errors;
using Imagino.Api.Models.Image;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Bson.IO;

namespace Imagino.Api.Services.Image.Providers
{
    public class ReplicateImageProviderClient : IImageProviderClient
    {
        public ImageProviderType ProviderType => ImageProviderType.Replicate;

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        private static readonly JsonWriterSettings RelaxedJsonWriterSettings = new()
        {
            OutputMode = JsonOutputMode.RelaxedExtendedJson
        };

        private static readonly IReadOnlyList<string> DefaultWebhookEvents = new[] { "completed" };

        public ReplicateImageProviderClient(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        public async Task<ProviderJobResult> CreateJobAsync(
            ImageModelProvider provider,
            ImageModelVersion version,
            BsonDocument resolvedParams)
        {
            var client = _httpClientFactory.CreateClient("ImageModelProvider");

            var baseUrl = _configuration["ReplicateSettings:BaseUrl"] ?? "https://api.replicate.com";
            var endpointUrl = BuildEndpointUrl(baseUrl, version.EndpointUrl);

            using var message = new HttpRequestMessage(HttpMethod.Post, endpointUrl);

            ApplyAuth(message);

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

            return new ProviderJobResult(jobId!, status.ToLowerInvariant());
        }

        private void ApplyAuth(HttpRequestMessage message)
        {
            var apiKey = _configuration["ReplicateSettings:ApiKey"];

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ValidationAppException("Configuration 'ReplicateSettings:ApiKey' is not configured");
            }

            message.Headers.Remove("Authorization");
            message.Headers.TryAddWithoutValidation("Authorization", $"Token {apiKey}");
        }

        private static string BuildEndpointUrl(string baseUrl, string endpointUrl)
        {
            if (Uri.TryCreate(endpointUrl, UriKind.Absolute, out var absolute))
            {
                return absolute.ToString();
            }

            return new Uri(new Uri(baseUrl, UriKind.Absolute), endpointUrl).ToString();
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
