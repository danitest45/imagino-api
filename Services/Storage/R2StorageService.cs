using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using Imagino.Api.Settings;

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

        public async Task<string> UploadAsync(Stream stream, string key, string contentType)
        {
            var request = new PutObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = key,
                InputStream = stream,
                ContentType = contentType,
                CannedACL = S3CannedACL.PublicRead
            };

            await _client.PutObjectAsync(request);
            return $"{_settings.PublicUrl}/{key}";
        }
    }
}
