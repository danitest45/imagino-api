using Imagino.Api.DTOs;
using Imagino.Api.Models;
using Imagino.Api.Repository;
using System.Text.RegularExpressions;

namespace Imagino.Api.Services.WebhookImage
{
    public class WebhookImageService(IImageJobRepository repository, ILogger<WebhookImageService> logger) : IWebhookImageService
    {
        private readonly IImageJobRepository _repository = repository;
        private readonly ILogger<WebhookImageService> _logger = logger;

        public async Task<RequestResult> ProcessarWebhookAsync(RunPodContentResponse payload)
        {
            var result = new RequestResult();

            _logger.LogInformation("🔁 Processando webhook: JobId={JobId}", payload.id);

            if (payload.status != "COMPLETED" || payload.output?.images?.Any() != true)
            {
                result.AddError("Payload incompleto ou job ainda não finalizado.");
                return result;
            }

            var job = await _repository.GetByJobIdAsync(payload.id);
            if (job == null)
            {
                result.AddError($"JobId '{payload.id}' não encontrado no banco.");
                return result;
            }

            var imageUrl = await SalvarImagemComoArquivoAsync(payload.output.images[0], payload.id);
            if (imageUrl == null)
            {
                result.AddError("Falha ao salvar a imagem em disco.");
                return result;
            }

            job.Status = "COMPLETED";
            job.ImageUrl = imageUrl;
            job.UpdatedAt = DateTime.UtcNow;

            await _repository.UpdateAsync(job);

            result.Content = new
            {
                job.JobId,
                job.Status,
                job.ImageUrl,
                job.UpdatedAt
            };

            _logger.LogInformation("✅ Job atualizado com sucesso: {JobId}", job.JobId);
            return result;
        }



        private async Task<string?> SalvarImagemComoArquivoAsync(string base64Data, string jobId)
        {
            try
            {
                // Remove prefixo base64
                var base64 = Regex.Replace(base64Data, @"^data:image\/[a-zA-Z]+;base64,", string.Empty);
                var bytes = Convert.FromBase64String(base64);

                // Garante a pasta wwwroot/images
                var pasta = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images");
                if (!Directory.Exists(pasta))
                    Directory.CreateDirectory(pasta);

                // Caminho e nome do arquivo
                var nomeArquivo = $"{jobId}.png";
                var caminho = Path.Combine(pasta, nomeArquivo);

                await File.WriteAllBytesAsync(caminho, bytes);

                return $"/images/{nomeArquivo}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao salvar imagem do job {JobId}", jobId);
                return null;
            }
        }

    }

}
