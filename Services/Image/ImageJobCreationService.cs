using System;
using System.Collections.Generic;
using System.Linq;
using Imagino.Api.DTOs;
using Imagino.Api.DTOs.Image;
using Imagino.Api.Errors;
using Imagino.Api.Models;
using Imagino.Api.Models.Image;
using Imagino.Api.Repositories.Image;
using Imagino.Api.Repository;
using Imagino.Api.Services.Image.Providers;
using MongoDB.Bson;

namespace Imagino.Api.Services.Image
{
    public class ImageJobCreationService : IImageJobCreationService
    {
        private readonly IModelResolverService _modelResolverService;
        private readonly IImageJobRepository _jobRepository;
        private readonly IUserRepository _userRepository;
        private readonly IImageModelProviderRepository _providerRepository;
        private readonly IReadOnlyDictionary<ImageProviderType, IImageProviderClient> _providerClients;

        public ImageJobCreationService(
            IModelResolverService modelResolverService,
            IImageJobRepository jobRepository,
            IUserRepository userRepository,
            IImageModelProviderRepository providerRepository,
            IEnumerable<IImageProviderClient> providerClients)
        {
            _modelResolverService = modelResolverService;
            _jobRepository = jobRepository;
            _userRepository = userRepository;
            _providerRepository = providerRepository;
            _providerClients = providerClients.ToDictionary(c => c.ProviderType);
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
            if (!_providerClients.TryGetValue(provider.ProviderType, out var client))
            {
                throw new ValidationAppException(
                    $"No image provider client registered for provider type '{provider.ProviderType}'");
            }

            var result = await client.CreateJobAsync(provider, version, resolvedParams);
            return (result.JobId, result.Status);
        }
    }
}
