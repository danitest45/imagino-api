using Imagino.Api.DTOs;
using Imagino.Api.Errors;
using Imagino.Api.Models;
using Imagino.Api.Repository;
using Imagino.Api.Settings;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Imagino.Api.Services.ImageGeneration
{
    public class ReplicateJobsService(
        HttpClient httpClient,
        IOptions<ReplicateSettings> settings,
        IImageJobRepository jobRepository,
        IUserRepository userRepository,
        IOptions<ImageGeneratorSettings> imageSettings) : IReplicateJobsService
    {
        private readonly HttpClient _httpClient = httpClient;
        private readonly ReplicateSettings _settings = settings.Value;
        private readonly IImageJobRepository _jobRepository = jobRepository;
        private readonly IUserRepository _userRepository = userRepository;
        private readonly ImageGeneratorSettings _imageSettings = imageSettings.Value;

        public async Task<JobCreatedResponse> GenerateImageAsync(ImageGenerationReplicateRequest request, string userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                throw new ValidationAppException("User not found");

            if (user.Credits < _imageSettings.ImageCost)
                throw new InsufficientCreditsException(user.Credits, _imageSettings.ImageCost);

            const string defaultModelKey = "flux";
            var requestedModelKey = string.IsNullOrWhiteSpace(request.Model) ? defaultModelKey : request.Model.Trim();
            requestedModelKey = requestedModelKey.ToLowerInvariant();
            var models = _settings.Models ?? new Dictionary<string, string>();

            if (models.Count == 0)
                throw new ValidationAppException("Replicate models are not configured.");

            var resolvedModelKey = requestedModelKey;
            if (!models.TryGetValue(resolvedModelKey, out var modelUrl) || string.IsNullOrWhiteSpace(modelUrl))
            {
                resolvedModelKey = defaultModelKey;
                if (!models.TryGetValue(resolvedModelKey, out modelUrl) || string.IsNullOrWhiteSpace(modelUrl))
                    throw new ValidationAppException($"Replicate model '{requestedModelKey}' is not configured.");
            }

            object replicateRequest;

            if (resolvedModelKey == "seedream-4")
            {
                var aspectRatio = string.IsNullOrWhiteSpace(request.AspectRatio) ? "4:3" : request.AspectRatio;

                replicateRequest = new
                {
                    input = new
                    {
                        size = "2K",
                        width = 2048,
                        height = 2048,
                        prompt = request.Prompt,
                        max_images = 1,
                        image_input = Array.Empty<object>(),
                        aspect_ratio = aspectRatio,
                        enhance_prompt = true,
                        sequential_image_generation = "disabled"
                    },
                    webhook = _settings.WebhookUrl,
                    webhook_events_filter = new[] { "completed" }
                };
            }
            else
            {
                (int steps, double guidance) = request.QualityLevel switch
                {
                    1 => (10, 2.0),
                    2 => (15, 2.5),
                    3 => (25, 3.0),
                    4 => (35, 4.0),
                    5 => (50, 5.0),
                    _ => (25, 3.0)
                };

                replicateRequest = new
                {
                    input = new
                    {
                        steps = steps,
                        width = 1024,
                        height = 1024,
                        prompt = request.Prompt,
                        guidance = guidance,
                        interval = 2,
                        aspect_ratio = request.AspectRatio,
                        output_format = "png",
                        output_quality = 100,
                        safety_tolerance = 2,
                        prompt_upsampling = false
                    },
                    webhook = _settings.WebhookUrl,
                    webhook_events_filter = new[] { "completed" }
                };
            }

            var json = JsonSerializer.Serialize(replicateRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", _settings.ApiKey);

            var response = await _httpClient.PostAsync(modelUrl, content);

            if (!response.IsSuccessStatusCode)
                throw new UpstreamServiceException("Replicate", response.StatusCode.ToString(), (int)response.StatusCode);

            var body = await response.Content.ReadAsStringAsync();
            var replicateResponse = JsonSerializer.Deserialize<ReplicateRawResponse>(body);

            var imageJob = new ImageJob
            {
                Prompt = request.Prompt,
                JobId = replicateResponse!.id,
                Status = replicateResponse.status.ToLower(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                UserId = userId,
                AspectRatio = request.AspectRatio,
                ImageUrls = new List<string>(),
                TokenConsumed = false,
                Model = resolvedModelKey
            };

            await _jobRepository.InsertAsync(imageJob);

            return new JobCreatedResponse
            {
                JobId = imageJob.JobId,
                Status = imageJob.Status,
            };
        }

    }

}
