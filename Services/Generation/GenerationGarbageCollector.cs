using Amazon.S3;
using Amazon.S3.Model;
using Imagino.Api.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Imagino.Api.Services.Generation;

public sealed record MediaGcCandidate(string Key, string Reason, DateTime LastModifiedUtc);
public sealed class GenerationGarbageCollector(IMongoClient mongo, IOptions<ImageGeneratorSettings> dbOptions, IOptions<R2StorageSettings> options)
{
    public async Task<object> DryRunAsync(CancellationToken ct)
    {
        if (dbOptions.Value.MongoDatabase != "imagino_staging" || options.Value.BucketNameVideos != "imagino-videos-staging")
            throw new Imagino.Api.Errors.ForbiddenFeatureException("GC is staging-only.");
        var settings = options.Value;
        using var s3 = new AmazonS3Client(settings.AccessKeyId, settings.SecretAccessKey,
            new AmazonS3Config { ServiceURL = settings.ServiceUrl, ForcePathStyle = true });
        var jobs = mongo.GetDatabase("imagino_staging").GetCollection<GenerationJob>("generation_jobs_v2");
        var candidates = new List<MediaGcCandidate>();
        var truncated = false;
        foreach (var prefix in new[] { "generation-v2/", "generation-inputs/" })
        {
            var page = await s3.ListObjectsV2Async(new ListObjectsV2Request { BucketName = settings.BucketNameVideos, Prefix = prefix, MaxKeys = 500 }, ct);
            truncated |= page.IsTruncated ?? false;
            foreach (var item in page.S3Objects ?? new())
            {
                if (item.LastModified == null || item.LastModified > DateTime.UtcNow.AddDays(-7)) continue;
                var parts = item.Key.Split('/');
                if (parts.Length < 3 || !MongoDB.Bson.ObjectId.TryParse(parts[1], out _)) continue;
                var id = Path.GetFileNameWithoutExtension(parts[2]);
                if (!MongoDB.Bson.ObjectId.TryParse(id, out _)) continue;
                var job = await jobs.Find(j => j.Id == id && j.UserId == parts[1]).FirstOrDefaultAsync(ct);
                // Retain every object belonging to any known job. This deliberately
                // favors false retention over deleting active/completed references.
                if (job != null) continue;
                candidates.Add(new(item.Key, "no_owner_job_and_older_than_7_days", item.LastModified.Value));
            }
        }
        return new { dryRun = true, deletionSupported = false, scannedAtUtc = DateTime.UtcNow, truncated, candidates };
    }
}
