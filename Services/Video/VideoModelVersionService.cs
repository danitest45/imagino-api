using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Imagino.Api.Errors;
using Imagino.Api.Models.Video;
using Imagino.Api.Repositories.Video;
using MongoDB.Bson;

namespace Imagino.Api.Services.Video
{
    public class VideoModelVersionService : IVideoModelVersionService
    {
        private static readonly string[] RequiredParameters = { "prompt", "max_output_length_seconds", "resolution" };

        private readonly IVideoModelVersionRepository _versionRepository;
        private readonly IVideoModelRepository _modelRepository;

        public VideoModelVersionService(IVideoModelVersionRepository versionRepository, IVideoModelRepository modelRepository)
        {
            _versionRepository = versionRepository;
            _modelRepository = modelRepository;
        }

        public async Task<List<VideoModelVersion>> ListByModelAsync(string modelId, VideoModelVersionStatus? status)
        {
            await EnsureModelExists(modelId);
            return await _versionRepository.GetByModelIdAsync(modelId, status);
        }

        public async Task<VideoModelVersion?> GetByModelAndTagAsync(string modelId, string versionTag)
        {
            return await _versionRepository.GetByModelAndTagAsync(modelId, versionTag);
        }

        public async Task<VideoModelVersion?> GetByIdAsync(string id)
        {
            return await _versionRepository.GetByIdAsync(id);
        }

        public async Task<VideoModelVersion> CreateAsync(VideoModelVersion version)
        {
            await EnsureModelExists(version.ModelId);
            await EnsureVersionTagUnique(version.ModelId, version.VersionTag);
            ValidateSchema(version.ParamSchema, version.Defaults);

            version.CreatedAt = DateTime.UtcNow;
            version.UpdatedAt = DateTime.UtcNow;

            await _versionRepository.InsertAsync(version);
            return version;
        }

        public async Task<VideoModelVersion?> UpdateAsync(string id, VideoModelVersion version)
        {
            var existing = await _versionRepository.GetByIdAsync(id);
            if (existing == null)
            {
                return null;
            }

            await EnsureModelExists(version.ModelId);
            if (!string.Equals(existing.VersionTag, version.VersionTag, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(existing.ModelId, version.ModelId, StringComparison.Ordinal))
            {
                await EnsureVersionTagUnique(version.ModelId, version.VersionTag);
            }

            ValidateSchema(version.ParamSchema, version.Defaults);

            existing.ModelId = version.ModelId;
            existing.VersionTag = version.VersionTag;
            existing.EndpointUrl = version.EndpointUrl;
            existing.ParamSchema = version.ParamSchema;
            existing.Defaults = version.Defaults;
            existing.Limits = version.Limits;
            existing.Pricing = version.Pricing;
            existing.Status = version.Status;
            existing.Rollout = version.Rollout;
            existing.ReleaseNotes = version.ReleaseNotes;
            existing.UpdatedAt = DateTime.UtcNow;

            await _versionRepository.UpdateAsync(existing);
            return existing;
        }

        public async Task DeleteAsync(string id)
        {
            await _versionRepository.DeleteAsync(id);
        }

        private async Task EnsureModelExists(string modelId)
        {
            var model = await _modelRepository.GetByIdAsync(modelId);
            if (model == null)
            {
                throw new ValidationAppException($"Model '{modelId}' not found");
            }
        }

        private async Task EnsureVersionTagUnique(string modelId, string versionTag)
        {
            var existing = await _versionRepository.GetByModelAndTagAsync(modelId, versionTag);
            if (existing != null)
            {
                throw new ValidationAppException($"Version tag '{versionTag}' already exists for model '{modelId}'");
            }
        }

        private static void ValidateSchema(BsonDocument? paramSchema, BsonDocument? defaults)
        {
            if (paramSchema == null)
            {
                throw new ValidationAppException("paramSchema is required and must define video generation parameters");
            }

            var properties = paramSchema.GetValue("properties", null) as BsonDocument;
            var requiredFields = paramSchema.GetValue("required", null)?.AsBsonArray.Select(v => v.AsString).ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (properties == null)
            {
                throw new ValidationAppException("paramSchema must include a properties object");
            }

            foreach (var field in RequiredParameters)
            {
                if (!properties.Contains(field))
                {
                    throw new ValidationAppException($"paramSchema must define '{field}'");
                }
            }

            if (requiredFields.Count > 0 && RequiredParameters.Any(f => !requiredFields.Contains(f)))
            {
                throw new ValidationAppException("paramSchema must require prompt, max_output_length_seconds, and resolution");
            }

            if (defaults != null)
            {
                foreach (var field in RequiredParameters)
                {
                    if (!defaults.Contains(field))
                    {
                        throw new ValidationAppException($"defaults must provide a value for '{field}'");
                    }
                }
            }
        }
    }
}
