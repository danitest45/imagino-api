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
}
public sealed class GenerationOutputStore : IGenerationOutputStore, IDisposable
{
    private readonly IAmazonS3 s3;
    private readonly R2StorageSettings settings;
    public GenerationOutputStore(IOptions<R2StorageSettings> options)
    {
        settings = options.Value;
        s3 = new AmazonS3Client(settings.AccessKeyId, settings.SecretAccessKey,
            new AmazonS3Config { ServiceURL = settings.ServiceUrl, ForcePathStyle = true });
    }
    public async Task<string> StoreAsync(GenerationJob job, byte[] bytes, CancellationToken ct)
    {
        var video = job.Model.MediaType == "video";
        var (extension, mime) = video ? IdentifyVideo(bytes) : GeneratedImageValidator.Identify(bytes);
        var key = Key(job, extension);
        using var stream = new MemoryStream(bytes, writable: false);
        await s3.PutObjectAsync(new PutObjectRequest { BucketName = video ? settings.BucketNameVideos : settings.BucketName,
            Key = key, InputStream = stream, ContentType = mime, DisablePayloadSigning = true, DisableDefaultChecksumValidation = true }, ct);
        return video ? "/api/generation/jobs/" + job.Id + "/download" : settings.PublicUrl.TrimEnd('/') + "/" + key;
    }
    public async Task<(byte[] Bytes, string ContentType)> DownloadAsync(GenerationJob job, CancellationToken ct)
    {
        var video = job.Model.MediaType == "video";
        var extension = video ? ".mp4" : Path.GetExtension(new Uri(job.OutputUrl!).AbsolutePath);
        if (extension is not (".png" or ".jpg" or ".webp" or ".mp4")) throw new InvalidDataException("Invalid output extension.");
        using var result = await s3.GetObjectAsync(video ? settings.BucketNameVideos : settings.BucketName, Key(job, extension), ct);
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
        return (output.ToArray(), result.Headers.ContentType);
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
