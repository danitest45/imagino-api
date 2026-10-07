using Amazon.S3;
using Amazon.S3.Model;
using Imagino.Api.Settings;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace Imagino.Api.Services.Generation;

public interface IGenerationInputStore
{
    Task StoreAsync(GenerationJob job, CancellationToken ct);
    Task LoadAsync(GenerationJob job, CancellationToken ct);
}
public sealed class GenerationInputStore : IGenerationInputStore, IDisposable
{
    private readonly IAmazonS3 s3;
    private readonly string bucket;
    public GenerationInputStore(IOptions<R2StorageSettings> options)
    {
        var settings = options.Value;
        bucket = settings.BucketNameVideos;
        s3 = new AmazonS3Client(settings.AccessKeyId, settings.SecretAccessKey,
            new AmazonS3Config { ServiceURL = settings.ServiceUrl, ForcePathStyle = true });
    }
    public async Task StoreAsync(GenerationJob job, CancellationToken ct)
    {
        var stored = new List<StoredGenerationInput>();
        for (var i = 0; i < job.Inputs.Count; i++)
        {
            var input = job.Inputs[i];
            var bytes = GenerationPolicy.ValidatePngInput(input.Data);
            var key = $"generation-inputs/{job.UserId}/{job.Id}/{i}.png";
            using var stream = new MemoryStream(bytes, writable: false);
            await s3.PutObjectAsync(new PutObjectRequest { BucketName = bucket, Key = key, InputStream = stream,
                ContentType = "image/png", DisablePayloadSigning = true, DisableDefaultChecksumValidation = true }, ct);
            stored.Add(new(input.Role, key, bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), input.SourceAssetId));
        }
        job.StoredInputs = stored;
        job.Inputs = new();
    }
    public async Task LoadAsync(GenerationJob job, CancellationToken ct)
    {
        if (job.StoredInputs.Count == 0) return; // historical bounded base64 compatibility
        var inputs = new List<GenerationInput>();
        for (var i = 0; i < job.StoredInputs.Count; i++)
        {
            var input = job.StoredInputs[i];
            if (input.Key != $"generation-inputs/{job.UserId}/{job.Id}/{i}.png" || input.Bytes is < 33 or > 2 * 1024 * 1024)
                throw new InvalidDataException("Invalid reference identity.");
            using var response = await s3.GetObjectAsync(bucket, input.Key, ct);
            if (response.ContentLength != input.Bytes) throw new InvalidDataException("Reference size mismatch.");
            var bytes = new byte[input.Bytes];
            await response.ResponseStream.ReadExactlyAsync(bytes, ct);
            if (await response.ResponseStream.ReadAsync(new byte[1], ct) != 0 ||
                input.Sha256 != Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant())
                throw new InvalidDataException("Reference integrity mismatch.");
            var data = "data:image/png;base64," + Convert.ToBase64String(bytes);
            GenerationPolicy.ValidatePngInput(data);
            inputs.Add(new(input.Role, data, input.SourceAssetId));
        }
        job.Inputs = inputs;
    }
    public void Dispose() => s3.Dispose();
}
