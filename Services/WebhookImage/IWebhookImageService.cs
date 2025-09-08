using Imagino.Api.DTOs;

namespace Imagino.Api.Services.WebhookImage
{
    public interface IWebhookImageService
    {
        Task<JobStatusResponse> ProcessarWebhookRunPodAsync(RunPodContentResponse payload);
        Task<JobStatusResponse> ProcessarWebhookReplicateAsync(ReplicateWebhookRequest payload);
    }
}