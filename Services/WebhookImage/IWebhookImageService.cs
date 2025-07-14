using Imagino.Api.DTOs;
using Imagino.Api.Models;

namespace Imagino.Api.Services.WebhookImage
{
    public interface IWebhookImageService
    {
        Task<RequestResult> ProcessarWebhookAsync(RunPodContentResponse payload);
    }
}