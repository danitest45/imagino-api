using System.Threading.Tasks;
using Imagino.Api.Models.Image;
using Imagino.Api.Models;
using MongoDB.Bson;

namespace Imagino.Api.Services.Image.Providers
{
    public record ProviderJobResult(string JobId, ImageJobStatus Status, string? ImageUrl = null);

    public interface IImageProviderClient
    {
        ImageProviderType ProviderType { get; }

        Task<ProviderJobResult> CreateJobAsync(
            ImageModelProvider provider,
            ImageModelVersion version,
            BsonDocument resolvedParams);
    }
}
