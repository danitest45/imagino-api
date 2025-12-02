using System.Text.Json;
using Imagino.Api.Models.Video;
using MongoDB.Bson;

namespace Imagino.Api.Services.Video
{
    public record ResolvedVideoPreset(VideoModel Model, VideoModelVersion Version, VideoModelPreset Preset, BsonDocument ResolvedParams);
    public record ResolvedVideoModelVersion(VideoModel Model, VideoModelVersion Version, BsonDocument ResolvedParams);

    public interface IVideoModelResolverService
    {
        Task<ResolvedVideoPreset> ResolvePresetAsync(string presetId, JsonDocument? requestParams);
        Task<ResolvedVideoModelVersion> ResolveModelAndVersionAsync(string modelSlug, string? versionTag, JsonDocument? requestParams);
    }
}
