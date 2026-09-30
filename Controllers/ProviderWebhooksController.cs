using Imagino.Api.DTOs;
using Imagino.Api.Security;
using Imagino.Api.Services.WebhookImage;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Imagino.Api.Controllers;

[ApiController]
[Route("api/webhooks")]
public class WebhooksController(IWebhookImageService service, IConfiguration config) : ControllerBase
{
    [HttpPost("runpod")]
    [RequestSizeLimit(16 * 1024 * 1024)]
    public async Task<IActionResult> RunPod()
    {
        // Native RunPod callbacks do not have a verified signing contract. A trusted
        // signing gateway/worker must be installed and validated before enabling this route.
        if (!config.GetValue<bool>("Webhooks:RunPodEnabled")) return StatusCode(503);
        return await ReceiveAsync<RunPodContentResponse>("Webhooks:RunPodSigningSecret", service.ProcessarWebhookRunPodAsync);
    }

    [HttpPost("replicate")]
    [RequestSizeLimit(1024 * 1024)]
    public Task<IActionResult> Replicate() => ReceiveAsync<ReplicateWebhookRequest>(
        "Webhooks:ReplicateSigningSecret", service.ProcessarWebhookReplicateAsync);

    private async Task<IActionResult> ReceiveAsync<T>(string key, Func<T, Task<JobStatusResponse>> process)
    {
        if (string.IsNullOrWhiteSpace(config[key])) return StatusCode(503);
        var max = typeof(T) == typeof(RunPodContentResponse) ? 16 * 1024 * 1024 : 1024 * 1024;
        using var body = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await Request.Body.ReadAsync(buffer, HttpContext.RequestAborted)) != 0)
        {
            if (body.Length + read > max) return StatusCode(413);
            body.Write(buffer, 0, read);
        }
        var bytes = body.ToArray();
        if (!WebhookVerifier.Verify(bytes, Request.Headers["webhook-id"], Request.Headers["webhook-timestamp"],
            Request.Headers["webhook-signature"], config[key], DateTimeOffset.UtcNow)) return Unauthorized();
        try
        {
            var payload = JsonSerializer.Deserialize<T>(bytes, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (payload == null) return BadRequest();
            return Ok(await process(payload));
        }
        catch (JsonException) { return BadRequest(); }
    }
}
