using Imagino.Api.DTOs;
using Imagino.Api.Errors;
using Imagino.Api.Models;
using Imagino.Api.Repository;
using Imagino.Api.Settings;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Linq;
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
        IOptions<ImageGeneratorSettings> imageSettings,
        IEnumerable<IReplicateModelRequestBuilder> builders) : IReplicateJobsService
    {
        private readonly HttpClient _httpClient = httpClient;
        private readonly ReplicateSettings _settings = settings.Value;
        private readonly IImageJobRepository _jobRepository = jobRepository;
        private readonly IUserRepository _userRepository = userRepository;
        private readonly ImageGeneratorSettings _imageSettings = imageSettings.Value;
        private readonly IReadOnlyDictionary<string, IReplicateModelRequestBuilder> _builders =
            (builders ?? Enumerable.Empty<IReplicateModelRequestBuilder>())
                .ToDictionary(b => b.ModelKey.ToLowerInvariant(), b => b);

        public async Task<JobCreatedResponse> GenerateImageAsync(ImageGenerationReplicateRequest request, string userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                throw new ValidationAppException("User not found");

            if (user.Credits < _imageSettings.ImageCost)
                throw new InsufficientCreditsException(user.Credits, _imageSettings.ImageCost);

            const string defaultModelKey = "flux";
            var requestedModelKey = string.IsNullOrWhiteSpace(request.Model) ? defaultModelKey : request.Model.Trim();
            var modelKey = requestedModelKey.ToLowerInvariant();
            var models = _settings.Models ?? new Dictionary<string, string>();

            if (models.Count == 0)
                throw new ValidationAppException("Replicate models are not configured.");

            if (!models.TryGetValue(modelKey, out var modelUrl) || string.IsNullOrWhiteSpace(modelUrl))
            {
                modelKey = defaultModelKey;
                if (!models.TryGetValue(modelKey, out modelUrl) || string.IsNullOrWhiteSpace(modelUrl))
                    throw new ValidationAppException($"Replicate model '{requestedModelKey}' is not configured.");
            }

            if (!_builders.TryGetValue(defaultModelKey, out var defaultBuilder))
                throw new ValidationAppException($"Replicate model '{defaultModelKey}' is not supported.");

            if (!_builders.TryGetValue(modelKey, out var builder))
            {
                builder = defaultBuilder;
                modelKey = defaultModelKey;
                modelUrl = models[modelKey];
            }

            var replicateRequest = builder.BuildRequest(request, _settings.WebhookUrl);

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
                Model = modelKey
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
