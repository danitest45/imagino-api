using Imagino.Api.DTOs;
using Imagino.Api.Models;

namespace Imagino.Api.Services.WebhookImage
{
    public interface IWebhookImageService
    {
        Task<RequestResult> ProcessarWebhookRunPodAsync(RunPodContentResponse payload);
        Task<RequestResult> ProcessarWebhookReplicateAsync(ReplicateWebhookRequest payload);
    }
}