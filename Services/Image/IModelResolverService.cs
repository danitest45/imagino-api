using System.Text.Json;
using Imagino.Api.Models.Image;
using MongoDB.Bson;

namespace Imagino.Api.Services.Image
{
    public record ResolvedPreset(ImageModel Model, ImageModelVersion Version, ImageModelPreset Preset, BsonDocument ResolvedParams);
    public record ResolvedModelVersion(ImageModel Model, ImageModelVersion Version, BsonDocument ResolvedParams);

    public interface IModelResolverService
    {
        Task<ResolvedPreset> ResolvePresetAsync(string presetId, JsonDocument? requestParams);
        Task<ResolvedModelVersion> ResolveModelAndVersionAsync(string modelSlug, string? versionTag, JsonDocument? requestParams);
    }
}
