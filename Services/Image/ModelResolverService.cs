using System.Collections.Generic;
using System.Text.Json;
using Imagino.Api.Errors;
using Imagino.Api.Models.Image;
using Imagino.Api.Repositories.Image;
using MongoDB.Bson;

namespace Imagino.Api.Services.Image
{
    public class ModelResolverService : IModelResolverService
    {
        private readonly IImageModelRepository _modelRepository;
        private readonly IImageModelVersionRepository _versionRepository;
        private readonly IImageModelPresetRepository _presetRepository;

        public ModelResolverService(
            IImageModelRepository modelRepository,
            IImageModelVersionRepository versionRepository,
            IImageModelPresetRepository presetRepository)
        {
            _modelRepository = modelRepository;
            _versionRepository = versionRepository;
            _presetRepository = presetRepository;
        }

        public async Task<ResolvedPreset> ResolvePresetAsync(string presetId, JsonDocument? requestParams)
        {
            var preset = await _presetRepository.GetByIdAsync(presetId)
                         ?? throw new ValidationAppException($"Preset '{presetId}' not found");

            if (preset.Status != ImageModelPresetStatus.Active)
            {
                throw new ValidationAppException($"Preset '{preset.Slug}' is not active");
            }

            var model = await _modelRepository.GetByIdAsync(preset.ModelId)
                        ?? throw new ValidationAppException($"Model '{preset.ModelId}' not found for preset '{preset.Slug}'");

            if (model.Status == ImageModelStatus.Archived)
            {
                throw new ValidationAppException($"Model '{model.Slug}' is archived");
            }

            var version = await _versionRepository.GetByIdAsync(preset.ModelVersionId)
                          ?? throw new ValidationAppException($"Version '{preset.ModelVersionId}' not found for preset '{preset.Slug}'");

            if (version.Status == ImageModelVersionStatus.Archived)
            {
                throw new ValidationAppException($"Version '{version.VersionTag}' is archived");
            }

            var resolvedParams = MergeAndValidate(version, preset, requestParams);
            return new ResolvedPreset(model, version, preset, resolvedParams);
        }

        public async Task<ResolvedModelVersion> ResolveModelAndVersionAsync(string modelSlug, string? versionTag, JsonDocument? requestParams)
        {
            if (string.IsNullOrWhiteSpace(modelSlug))
            {
                throw new ValidationAppException("Model slug must be provided");
            }

            var model = await _modelRepository.GetBySlugAsync(modelSlug)
                        ?? throw new ValidationAppException($"Model '{modelSlug}' not found");

            if (model.Status == ImageModelStatus.Archived)
            {
                throw new ValidationAppException($"Model '{modelSlug}' is archived");
            }

            ImageModelVersion? version;

            if (!string.IsNullOrWhiteSpace(versionTag))
            {
                version = await _versionRepository.GetByModelAndTagAsync(model.Id!, versionTag)
                    ?? throw new ValidationAppException($"Version '{versionTag}' not found for model '{modelSlug}'");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(model.DefaultVersionId))
                {
                    throw new ValidationAppException($"Model '{modelSlug}' does not have a default version configured");
                }

                version = await _versionRepository.GetByIdAsync(model.DefaultVersionId)
                    ?? throw new ValidationAppException($"Default version '{model.DefaultVersionId}' not found for model '{modelSlug}'");
            }

            if (version.Status == ImageModelVersionStatus.Archived)
            {
                throw new ValidationAppException($"Version '{version.VersionTag}' is archived");
            }

            var resolvedParams = MergeAndValidate(version, null, requestParams);
            return new ResolvedModelVersion(model, version, resolvedParams);
        }

        private BsonDocument MergeAndValidate(ImageModelVersion version, ImageModelPreset? preset, JsonDocument? requestParams)
        {
            var merged = version.Defaults != null
                ? new BsonDocument(version.Defaults)
                : new BsonDocument();

            MergeDocuments(merged, preset?.Params, allowOverrideLocked: true, locks: null);

            HashSet<string>? locks = preset?.Locks != null
                ? new HashSet<string>(preset.Locks)
                : null;

            var requestDocument = ConvertJsonToBson(requestParams);
            MergeDocuments(merged, requestDocument, allowOverrideLocked: false, locks: locks);

            ValidateAgainstSchema(version.ParamSchema, merged);

            return merged;
        }

        private static BsonDocument ConvertJsonToBson(JsonDocument? document)
        {
            if (document == null)
            {
                return new BsonDocument();
            }

            var json = document.RootElement.GetRawText();
            return string.IsNullOrWhiteSpace(json) || json == "null"
                ? new BsonDocument()
                : BsonDocument.Parse(json);
        }

        private static void MergeDocuments(BsonDocument target, BsonDocument? source, bool allowOverrideLocked, HashSet<string>? locks)
        {
            if (source == null)
            {
                return;
            }

            foreach (var element in source)
            {
                var key = element.Name;
                if (!allowOverrideLocked && locks != null && locks.Contains(key) && target.Contains(key))
                {
                    continue;
                }

                if (element.Value.IsBsonDocument)
                {
                    var existing = target.Contains(key) && target[key].IsBsonDocument
                        ? new BsonDocument(target[key].AsBsonDocument)
                        : new BsonDocument();

                    MergeDocuments(existing, element.Value.AsBsonDocument, allowOverrideLocked, locks);
                    target[key] = existing;
                }
                else
                {
                    target[key] = element.Value.DeepClone();
                }
            }
        }

        private static void ValidateAgainstSchema(BsonDocument? schema, BsonDocument document)
        {
            if (schema == null)
            {
                return;
            }

            if (schema.TryGetValue("required", out var requiredElement) && requiredElement.IsBsonArray)
            {
                foreach (var required in requiredElement.AsBsonArray)
                {
                    var field = required.AsString;
                    if (!document.Contains(field) || document[field].IsBsonNull)
                    {
                        throw new ValidationAppException($"Missing required parameter '{field}'");
                    }
                }
            }
        }
    }
}
