using Amazon.S3;
using Amazon.S3.Model;
using Imagino.Api.Security;
using Imagino.Api.Settings;
using Microsoft.Extensions.Options;

namespace Imagino.Api.Services.Generation;

public interface IGenerationOutputStore
{
    Task<string> StoreAsync(GenerationJob job, byte[] bytes, CancellationToken ct);
    Task<(byte[] Bytes, string ContentType)> DownloadAsync(GenerationJob job, CancellationToken ct);
    Task<bool> TryRecoverAsync(GenerationJob job, CancellationToken ct) => Task.FromResult(false);
}
public sealed class GenerationOutputStore : IGenerationOutputStore, IDisposable
{
    private readonly IAmazonS3 s3;
    private readonly R2StorageSettings settings;
    public GenerationOutputStore(IOptions<R2StorageSettings> options, IAmazonS3? client = null)
    {
        settings = options.Value;
        s3 = client ?? new AmazonS3Client(settings.AccessKeyId, settings.SecretAccessKey,
            new AmazonS3Config { ServiceURL = settings.ServiceUrl, ForcePathStyle = true });
    }
    public async Task<string> StoreAsync(GenerationJob job, byte[] bytes, CancellationToken ct)
    {
        var video = job.Model.MediaType == "video";
        var (extension, mime) = video ? IdentifyVideo(bytes) : GeneratedImageValidator.Identify(bytes);
        var key = Key(job, extension);
        using var stream = new MemoryStream(bytes, writable: false);
        // This bucket already serves private video. New V2 images share the private
        // namespace; the legacy public image bucket is read-only compatibility.
        await s3.PutObjectAsync(new PutObjectRequest { BucketName = settings.BucketNameVideos,
            Key = key, InputStream = stream, ContentType = mime, DisablePayloadSigning = true, DisableDefaultChecksumValidation = true }, ct);
        job.StoredOutput = new(key, mime, bytes.Length,
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant());
        job.OutputStoredAtUtc = DateTime.UtcNow;
        return $"/api/generation/jobs/{job.Id}/media";
    }
    public async Task<(byte[] Bytes, string ContentType)> DownloadAsync(GenerationJob job, CancellationToken ct)
    {
        var video = job.Model.MediaType == "video";
        var extension = job.StoredOutput != null ? Path.GetExtension(job.StoredOutput.Key) :
            video ? ".mp4" : LegacyExtension(job);
        if (extension is not (".png" or ".jpg" or ".webp" or ".mp4")) throw new InvalidDataException("Invalid output extension.");
        var key = Key(job, extension);
        if (job.StoredOutput != null && job.StoredOutput.Key != key) throw new InvalidDataException("Invalid output identity.");
        return await ReadAsync(job, job.StoredOutput != null || video ? settings.BucketNameVideos : settings.BucketName, key, ct);
    }
    public async Task<bool> TryRecoverAsync(GenerationJob job, CancellationToken ct)
    {
        // Exact server-owned private keys only. Recover the PutObject → DB crash window.
        foreach (var extension in job.Model.MediaType == "video" ? new[] { ".mp4" } : new[] { ".png", ".jpg", ".webp" })
        {
            var key = Key(job, extension);
            try {
                var (bytes, mime) = await ReadAsync(job, settings.BucketNameVideos, key, ct);
                job.StoredOutput = new(key, mime, bytes.Length, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant());
                job.OutputStoredAtUtc = DateTime.UtcNow;
                return true;
            } catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound) { }
        }
        return false;
    }
    private async Task<(byte[] Bytes, string ContentType)> ReadAsync(GenerationJob job, string bucket, string key, CancellationToken ct)
    {
        var video = job.Model.MediaType == "video";
        using var result = await s3.GetObjectAsync(bucket, key, ct);
        var limit = video ? 100 * 1024 * 1024 : GeneratedImageValidator.MaxBytes;
        if (result.ContentLength > limit) throw new InvalidDataException("Stored output exceeds limit.");
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await result.ResponseStream.ReadAsync(buffer, ct)) > 0)
        {
            if (output.Length + read > limit) throw new InvalidDataException("Stored output exceeds limit.");
            await output.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        var bytes = output.ToArray();
        var (_, mime) = video ? IdentifyVideo(bytes) : GeneratedImageValidator.Identify(bytes);
        if (job.StoredOutput != null && (job.StoredOutput.Bytes != bytes.Length || job.StoredOutput.ContentType != mime ||
            job.StoredOutput.Sha256 != Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant()))
            throw new InvalidDataException("Stored output integrity mismatch.");
        return (bytes, mime);
    }
    internal static string LegacyExtension(GenerationJob job)
    {
        if (!Uri.TryCreate(job.OutputUrl, UriKind.Absolute, out var uri) ||
            !uri.AbsolutePath.EndsWith($"/generation-v2/{job.UserId}/{job.Id}{Path.GetExtension(uri.AbsolutePath)}", StringComparison.Ordinal))
            throw new InvalidDataException("Historical output identity is unavailable.");
        return Path.GetExtension(uri.AbsolutePath);
    }
    private static string Key(GenerationJob job, string extension) => $"generation-v2/{job.UserId}/{job.Id}{extension}";
    private static (string, string) IdentifyVideo(byte[] bytes)
    {
        if (bytes.Length < 12 || bytes.Length > 100 * 1024 * 1024 || !bytes.AsSpan(4, 4).SequenceEqual("ftyp"u8))
            throw new InvalidDataException("Expected MP4 output up to 100 MiB.");
        return (".mp4", "video/mp4");
    }
    public void Dispose() => s3.Dispose();
}
