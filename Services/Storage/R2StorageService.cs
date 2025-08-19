using Amazon;
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
            var prop = typeof(AWSConfigsS3).GetProperty("UseSignatureVersion4");
            prop?.SetValue(null, true);
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
                DisablePayloadSigning = true,
                DisableDefaultChecksumValidation = true
            };

            if (stream.CanSeek)
            {
                stream.Position = 0;
                request.Headers.ContentLength = stream.Length;
            }


            await _client.PutObjectAsync(request);
            return $"{_settings.PublicUrl}/{key}";
        }

        public Task<string> GetDownloadUrlAsync(string key, string fileName)
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _settings.BucketName,
                Key = key,
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.AddMinutes(5)
            };
            request.ResponseHeaderOverrides.ContentDisposition = $"attachment; filename=\"{fileName}\"";
            var url = _client.GetPreSignedURL(request);
            return Task.FromResult(url);
        }
    }
}
