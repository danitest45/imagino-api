using Imagino.Api.DTOs;
using Imagino.Api.Errors;
using Imagino.Api.Models;
using Imagino.Api.Repository;
using Imagino.Api.Security;
using Imagino.Api.Services.Storage;

namespace Imagino.Api.Services.WebhookImage;

public class WebhookImageService(IImageJobRepository jobs, IUserRepository users, IStorageService storage,
    SafeMediaDownloader downloader, IConfiguration? config = null) : IWebhookImageService
{
    public Task<JobStatusResponse> ProcessarWebhookRunPodAsync(RunPodContentResponse payload) =>
        ProcessAsync(payload.id, "RunPod", payload.status == "COMPLETED", async () =>
        {
            var encoded = payload.output?.images?.FirstOrDefault() ?? throw new ValidationAppException("Missing provider output.");
            var comma = encoded.StartsWith("data:image/", StringComparison.Ordinal) ? encoded.IndexOf(',') : -1;
            var base64 = comma >= 0 ? encoded[(comma + 1)..] : encoded;
            if (base64.Length > GeneratedImageValidator.MaxBase64Chars) throw new ValidationAppException("Provider output too large.");
            return Convert.FromBase64String(base64);
        });

    public Task<JobStatusResponse> ProcessarWebhookReplicateAsync(ReplicateWebhookRequest payload) =>
        ProcessAsync(payload.Id, "Replicate", payload.Status == "succeeded", () =>
            downloader.DownloadAsync(payload.Output ?? "", StagingWebhookFixtures.ReplicateHosts(config), GeneratedImageValidator.MaxBytes));

    private async Task<JobStatusResponse> ProcessAsync(string providerId, string provider, bool succeeded, Func<Task<byte[]>> download)
    {
        if (string.IsNullOrWhiteSpace(providerId) || providerId.Length > 200) throw new ValidationAppException("Invalid provider job.");
        var job = await jobs.GetByProviderJobIdAsync(providerId);
        if (job == null || job.CallbackProvider != provider || string.IsNullOrEmpty(job.UserId) ||
            !job.TokenConsumed || await users.GetByIdAsync(job.UserId) == null)
            throw new ValidationAppException("Provider job or owner is not eligible for callbacks.");
        if (job.Status == ImageJobStatus.Completed) return Response(job);
        if (!succeeded || !CanComplete(job.Status)) throw new ValidationAppException("Invalid provider status transition.");
        var lease = TokenSecurity.RandomToken();
        if (!await jobs.TryClaimWebhookAsync(job, lease)) throw new ValidationAppException("Callback is already processing.");
        try
        {
            var bytes = await download();
            var format = GeneratedImageValidator.Identify(bytes);
            using var stream = new MemoryStream(bytes, writable: false);
            // Persisted local job id determines the key, never a provider-supplied path.
            var url = await storage.UploadAsync(stream, $"images/{job.Id}{format.Extension}", format.ContentType);
            if (!await jobs.CompleteWebhookAsync(job.Id!, lease, url)) throw new ValidationAppException("Callback lease expired.");
            job.Status = ImageJobStatus.Completed;
            job.ImageUrls = new() { url };
            job.UpdatedAt = DateTime.UtcNow;
            return Response(job);
        }
        catch
        {
            await jobs.ReleaseWebhookAsync(job.Id!, lease);
            throw;
        }
    }

    public static bool CanComplete(ImageJobStatus status) => status is ImageJobStatus.Queued or ImageJobStatus.Running or ImageJobStatus.Starting or ImageJobStatus.Processing;
    private static JobStatusResponse Response(ImageJob job) => new() {
        JobId = job.JobId, Status = job.Status.ToString(), ImageUrls = job.ImageUrls, UpdatedAt = job.UpdatedAt
    };
}
