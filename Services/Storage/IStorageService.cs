using System.IO;
using System.Threading;

namespace Imagino.Api.Services.Storage
{
    public interface IStorageService
    {
        Task<string> UploadAsync(Stream stream, string key, string contentType, CancellationToken cancellationToken = default);

        Task<string> UploadVideoAsync(Stream stream, string fileName, CancellationToken cancellationToken = default);
    }
}
