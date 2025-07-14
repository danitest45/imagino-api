using Imagino.Api.DTOs;
using Imagino.Api.Services.WebhookImage;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("webhook")]
public class WebhookController : ControllerBase
{
    private readonly ILogger<WebhookController> _logger;
    private readonly WebhookImageService _webhookService;

    public WebhookController(ILogger<WebhookController> logger, WebhookImageService webhookImageService)
    {
        _logger = logger;
        _webhookService = webhookImageService;
    }

    [HttpPost("runpod")]
    public IActionResult ReceiveRunPodWebhook([FromBody] RunPodContentResponse payload)
    {
        _logger.LogInformation("🔔 Webhook recebido do RunPod: JobId={JobId}, Status={Status}", payload.id, payload.status);

        if (payload.status == "COMPLETED" && payload.output?.images?.Any() == true)
        {
            var base64Image = payload.output.images[0];

            _logger.LogInformation("🖼️ Imagem base64 gerada para visualização.");

            // Retorna diretamente a imagem embutida em base64
            return Ok(new { imageBase64 = base64Image });
        }

        return Ok(new { message = "Imagem não disponível ou job incompleto." });
    }
}
