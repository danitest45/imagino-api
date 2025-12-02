using System.Threading.Tasks;
using Imagino.Api.Models;
using Imagino.Api.Models.Video;
using MongoDB.Bson;

namespace Imagino.Api.Services.Video.Providers
{
    public record VideoProviderJobResult(string JobId, VideoJobStatus Status, string? VideoUrl = null);

    public interface IVideoProviderClient
    {
        VideoProviderType ProviderType { get; }

        Task<VideoProviderJobResult> CreateJobAsync(VideoModelProvider provider, VideoModelVersion version, BsonDocument resolvedParams);

        Task<VideoProviderJobResult> PollResultAsync(VideoModelProvider provider, VideoModelVersion version, string providerJobId, BsonDocument resolvedParams);
    }
}
