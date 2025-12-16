using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using Imagino.Api.Settings;
using System.IO;
using System.Threading;

namespace Imagino.Api.Services.Storage
{
    public class R2StorageService : IStorageService
    {
        private readonly IAmazonS3 _client;
        private readonly R2StorageSettings _settings;

        public R2StorageService(IOptions<R2StorageSettings> options)
        {
            _settings = options.Value;
            var config = new AmazonS3Config
            {
                ServiceURL = _settings.ServiceUrl,
                ForcePathStyle = true
            };
            _client = new AmazonS3Client(_settings.AccessKeyId, _settings.SecretAccessKey, config);
        }

        public async Task<string> UploadAsync(Stream stream, string key, string contentType, CancellationToken cancellationToken = default)
        {
            var bucketName = _settings.BucketName;
            return await UploadInternalAsync(stream, bucketName, key, contentType, cancellationToken);
        }

        public async Task<string> UploadVideoAsync(Stream stream, string fileName, CancellationToken cancellationToken = default)
        {
            var key = $"videos/{fileName}";
            return await UploadInternalAsync(stream, _settings.BucketNameVideos, key, "video/mp4", cancellationToken);
        }

        private async Task<string> UploadInternalAsync(Stream stream, string bucketName, string key, string contentType, CancellationToken cancellationToken)
        {
            var request = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = key,
                InputStream = stream,
                ContentType = contentType,
                DisablePayloadSigning = true,
                DisableDefaultChecksumValidation = true
            };

            if (stream.CanSeek)
            {
                stream.Position = 0;
                request.Headers.ContentLength = stream.Length;
            }


            await _client.PutObjectAsync(request, cancellationToken);
            return BuildPublicUrl(bucketName, key);
        }

        private string BuildPublicUrl(string bucketName, string key)
        {
            var baseUrl = _settings.PublicUrl.TrimEnd('/');
            return $"{baseUrl}/images/{key}";
        }
    }
}
