using Imagino.Api.DTOs;
using Imagino.Api.Services.WebhookImage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Imagino.Api.Controllers;

/// <summary>
/// Handles webhook callbacks from third-party services such as RunPod.
/// </summary>
[ApiController]
[Route("api/webhooks")]
public class WebhooksController(ILogger<WebhooksController> logger, IWebhookImageService webhookService) : ControllerBase
{
    private readonly ILogger<WebhooksController> _logger = logger;
    private readonly IWebhookImageService _webhookService = webhookService;

    /// <summary>
    /// Receives a callback from RunPod when a job is completed.
    /// </summary>
    /// <param name="payload">Payload sent by RunPod with job details.</param>
    /// <returns>A standardized result with job status and image URL if available.</returns>
    /// <response code="200">Webhook processed successfully.</response>
    /// <response code="400">Failed to process the webhook payload.</response>
    [HttpPost("runpod")]
    [ProducesResponseType(typeof(JobStatusResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReceiveRunPodWebhook([FromBody] RunPodContentResponse payload)
    {
        _logger.LogInformation("🔔 Webhook received: JobId={JobId}, Status={Status}", payload.id, payload.status);

        var result = await _webhookService.ProcessarWebhookRunPodAsync(payload);

        return Ok(result);
    }

    /// <summary>
    /// Receives a callback from Replicate when a job is completed.
    /// </summary>
    /// <param name="payload">Payload sent by Replicate with job details.</param>
    /// <returns>A standardized result with job status and image URL if available.</returns>
    /// <response code="200">Webhook processed successfully.</response>
    /// <response code="400">Failed to process the webhook payload.</response>
    [HttpPost("replicate")]
    [ProducesResponseType(typeof(JobStatusResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReceiveReplicateWebhook([FromBody] ReplicateWebhookRequest payload)
    {
        _logger.LogInformation("🔔 Webhook Replicate recebido: JobId={JobId}, Status={Status}", payload.Id, payload.Status);

        var result = await _webhookService.ProcessarWebhookReplicateAsync(payload);

        return Ok(result);
    }
}
