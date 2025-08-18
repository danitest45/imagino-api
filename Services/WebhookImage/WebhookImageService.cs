using Imagino.Api.DTOs;
using Imagino.Api.Models;
using Imagino.Api.Repository;
using Imagino.Api.Settings;
using Imagino.Api.Services.Storage;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;
using System.IO;

namespace Imagino.Api.Services.WebhookImage
{
    public class WebhookImageService(IImageJobRepository repository, IUserRepository userRepository, ILogger<WebhookImageService> logger, IOptions<ImageGeneratorSettings> settings, IStorageService storage) : IWebhookImageService
    {
        private readonly IImageJobRepository _repository = repository;
        private readonly IUserRepository _userRepository = userRepository;
        private readonly ILogger<WebhookImageService> _logger = logger;
        private readonly ImageGeneratorSettings _settings = settings.Value;
        private readonly IStorageService _storage = storage;

        public async Task<RequestResult> ProcessarWebhookRunPodAsync(RunPodContentResponse payload)
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

            var imageUrl = await SalvarImagemAsync(payload.output.images[0], payload.id);
            if (imageUrl == null)
            {
                result.AddError("Falha ao salvar a imagem em disco.");
                return result;
            }

            job.Status = "COMPLETED";
            job.ImageUrls.Add(imageUrl);
            job.UpdatedAt = DateTime.UtcNow;

            if (!job.TokenConsumed && !string.IsNullOrEmpty(job.UserId))
            {
                if (await _userRepository.DecrementCreditsAsync(job.UserId, _settings.ImageCost))
                    job.TokenConsumed = true;
                else
                    _logger.LogWarning("Não foi possível debitar crédito do usuário {UserId}", job.UserId);
            }

            await _repository.UpdateAsync(job);

            result.Content = new
            {
                job.JobId,
                job.Status,
                job.ImageUrls,
                job.UpdatedAt
            };

            _logger.LogInformation("✅ Job atualizado com sucesso: {JobId}", job.JobId);
            return result;
        }



        private async Task<string?> SalvarImagemAsync(string base64Data, string jobId)
        {
            try
            {
                var base64 = Regex.Replace(base64Data, @"^data:image\/[a-zA-Z]+;base64,", string.Empty);
                var bytes = Convert.FromBase64String(base64);
                using var ms = new MemoryStream(bytes);
                var key = $"images/{jobId}.png";
                return await _storage.UploadAsync(ms, key, "image/png");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao salvar imagem do job {JobId}", jobId);
                return null;
            }
        }

        public async Task<RequestResult> ProcessarWebhookReplicateAsync(ReplicateWebhookRequest payload)
        {
            var result = new RequestResult();

            _logger.LogInformation("🔁 Processando webhook Replicate: JobId={JobId}", payload.Id);

            if (string.IsNullOrWhiteSpace(payload.Status) || payload.Status.ToLower() != "succeeded" || string.IsNullOrWhiteSpace(payload.Output))
            {
                result.AddError("Payload incompleto ou job ainda não finalizado.");
                return result;
            }

            var job = await _repository.GetByJobIdAsync(payload.Id);
            if (job == null)
            {
                result.AddError($"JobId '{payload.Id}' não encontrado no banco.");
                return result;
            }

            var imageUrl = await BaixarImagemReplicateAsync(payload.Output, payload.Id);
            if (imageUrl == null)
            {
                result.AddError("Erro ao baixar a imagem.");
                return result;
            }

            job.Status = "COMPLETED";
            job.ImageUrls.Add(imageUrl);
            job.UpdatedAt = DateTime.UtcNow;

            if (!job.TokenConsumed && !string.IsNullOrEmpty(job.UserId))
            {
                if (await _userRepository.DecrementCreditsAsync(job.UserId, _settings.ImageCost))
                    job.TokenConsumed = true;
                else
                    _logger.LogWarning("Não foi possível debitar crédito do usuário {UserId}", job.UserId);
            }

            await _repository.UpdateAsync(job);

            result.Content = new
            {
                job.JobId,
                job.Status,
                job.ImageUrls,
                job.UpdatedAt
            };

            _logger.LogInformation("✅ Job Replicate finalizado com sucesso: {JobId}", job.JobId);
            return result;
        }

        private async Task<string?> BaixarImagemReplicateAsync(string imageUrl, string jobId)
        {
            try
            {
                using var client = new HttpClient();
                var bytes = await client.GetByteArrayAsync(imageUrl);
                using var ms = new MemoryStream(bytes);
                var key = $"images/{jobId}.png";
                return await _storage.UploadAsync(ms, key, "image/png");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao baixar imagem do Replicate para o job {JobId}", jobId);
                return null;
            }
        }

    }

}
