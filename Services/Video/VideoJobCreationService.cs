using System;
using System.Collections.Generic;
using System.Linq;
using Imagino.Api.DTOs;
using Imagino.Api.DTOs.Video;
using Imagino.Api.Errors;
using Imagino.Api.Models;
using Imagino.Api.Models.Video;
using Imagino.Api.Repositories.Video;
using Imagino.Api.Repository;
using Imagino.Api.Services.Video.Providers;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;

namespace Imagino.Api.Services.Video
{
    public class VideoJobCreationService : IVideoJobCreationService
    {
        private readonly IVideoModelResolverService _modelResolverService;
        private readonly IVideoJobRepository _jobRepository;
        private readonly IUserRepository _userRepository;
        private readonly IVideoModelProviderRepository _providerRepository;
        private readonly IReadOnlyDictionary<VideoProviderType, IVideoProviderClient> _providerClients;
        private readonly ILogger<VideoJobCreationService> _logger;

        public VideoJobCreationService(
            IVideoModelResolverService modelResolverService,
            IVideoJobRepository jobRepository,
            IUserRepository userRepository,
            IVideoModelProviderRepository providerRepository,
            IEnumerable<IVideoProviderClient> providerClients,
            ILogger<VideoJobCreationService> logger)
        {
            _modelResolverService = modelResolverService;
            _jobRepository = jobRepository;
            _userRepository = userRepository;
            _providerRepository = providerRepository;
            _providerClients = providerClients.ToDictionary(c => c.ProviderType);
            _logger = logger;
        }

        public async Task<JobCreatedResponse> CreateJobAsync(CreateVideoJobRequest request, string userId)
        {
            var user = await _userRepository.GetByIdAsync(userId)
                       ?? throw new ValidationAppException("User not found");

            ResolvedVideoPreset? resolvedPreset = null;
            ResolvedVideoModelVersion? resolvedModelVersion = null;

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

            var provider = await _providerRepository.GetByIdAsync(model.ProviderId)
                           ?? throw new ValidationAppException($"Provider '{model.ProviderId}' not found");

            var pricing = version.Pricing ?? model.Pricing;
            if (pricing == null)
            {
                throw new ValidationAppException("Pricing not configured for the selected model version");
            }

            var durationSeconds = ResolveDurationSeconds(resolvedParams);
            var creditsToConsume = CalculateCredits(pricing, durationSeconds);

            if (creditsToConsume <= 0)
            {
                throw new ValidationAppException("Pricing is misconfigured for the selected model version");
            }

            if (!await _userRepository.DecrementCreditsAsync(userId, creditsToConsume))
            {
                throw new InsufficientCreditsException(user.Credits, creditsToConsume);
            }

            try
            {
                var prompt = resolvedParams.TryGetValue("prompt", out var promptValue) && promptValue.IsString
                    ? promptValue.AsString
                    : null;

                var now = DateTime.UtcNow;
                var job = new VideoJob
                {
                    Id = ObjectId.GenerateNewId().ToString(),
                    Prompt = prompt,
                    JobId = null,
                    Status = VideoJobStatus.Created,
                    UserId = userId,
                    ModelSlug = model.Slug,
                    VersionTag = version.VersionTag,
                    PresetId = resolvedPreset?.Preset.Id ?? request.PresetId,
                    ResolvedParams = resolvedParams,
                    CreatedAt = now,
                    UpdatedAt = now,
                    TokenConsumed = true,
                    DurationSeconds = durationSeconds
                };

                job.JobId = job.Id;

                await _jobRepository.InsertAsync(job);

                await DispatchAsync(job, provider, version, resolvedParams);

                return new JobCreatedResponse
                {
                    JobId = job.Id,
                    Status = job.Status.ToString(),
                    CreatedAt = job.CreatedAt,
                    Model = model.Slug,
                    Version = version.VersionTag,
                    PresetId = job.PresetId
                };
            }
            catch
            {
                await _userRepository.IncrementCreditsAsync(userId, creditsToConsume);
                throw;
            }
        }

        private async Task DispatchAsync(VideoJob job, VideoModelProvider provider, VideoModelVersion version, BsonDocument resolvedParams)
        {
            try
            {
                var result = await DispatchToProviderAsync(provider, version, resolvedParams);

                job.ProviderJobId = result.JobId;
                job.Status = result.Status;
                job.VideoUrl = result.VideoUrl ?? job.VideoUrl;
                job.UpdatedAt = DateTime.UtcNow;

                await _jobRepository.UpdateAsync(job);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create video job for provider {Provider}", provider.Name);

                job.Status = VideoJobStatus.Failed;
                job.ErrorMessage = ex.Message;
                job.UpdatedAt = DateTime.UtcNow;

                await _jobRepository.UpdateAsync(job);
                throw;
            }
        }

        private async Task<VideoProviderJobResult> DispatchToProviderAsync(VideoModelProvider provider, VideoModelVersion version, BsonDocument resolvedParams)
        {
            if (!_providerClients.TryGetValue(provider.ProviderType, out var client))
            {
                throw new ValidationAppException(
                    $"No video provider client registered for provider type '{provider.ProviderType}'");
            }

            return await client.CreateJobAsync(provider, version, resolvedParams);
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

        private static int CalculateCredits(VideoModelPricing pricing, int durationSeconds)
        {
            if (pricing.CreditsPerVideo > 0)
            {
                return pricing.CreditsPerVideo;
            }

            if (pricing.CreditsPerSecond <= 0)
            {
                return 0;
            }

            var seconds = durationSeconds > 0 ? durationSeconds : 1;
            var computed = (int)Math.Ceiling((double)(seconds * pricing.CreditsPerSecond));

            if (pricing.MinCredits > 0)
            {
                computed = Math.Max(computed, pricing.MinCredits);
            }

            return computed;
        }
    }
}
