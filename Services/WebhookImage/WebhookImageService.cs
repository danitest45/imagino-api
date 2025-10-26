using Imagino.Api.DTOs;
using Imagino.Api.Errors;
using System.Linq;
using Imagino.Api.Repository;
using System;
using Imagino.Api.Settings;
using Imagino.Api.Services.Storage;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;
using System.IO;
using Imagino.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using Imagino.Api.Models;

namespace Imagino.Api.Services.WebhookImage
{
    public class WebhookImageService(IImageJobRepository repository, IUserRepository userRepository, ILogger<WebhookImageService> logger, IOptions<ImageGeneratorSettings> settings, IStorageService storage, IHubContext<JobHub> hubContext) : IWebhookImageService
    {
        private readonly IImageJobRepository _repository = repository;
        private readonly IUserRepository _userRepository = userRepository;
        private readonly ILogger<WebhookImageService> _logger = logger;
        private readonly ImageGeneratorSettings _settings = settings.Value;
        private readonly IStorageService _storage = storage;
        private readonly IHubContext<JobHub> _hubContext = hubContext;

        public async Task<JobStatusResponse> ProcessarWebhookRunPodAsync(RunPodContentResponse payload)
        {
            _logger.LogInformation("🔁 Processando webhook: JobId={JobId}", payload.id);

            if (payload.status != "COMPLETED" || payload.output?.images?.Any() != true)
                throw new ValidationAppException("Payload incompleto ou job ainda não finalizado.");

            var job = await _repository.GetByJobIdAsync(payload.id);
            if (job == null)
                throw new ValidationAppException($"JobId '{payload.id}' não encontrado no banco.");

            var imageUrl = await SalvarImagemAsync(payload.output.images[0], payload.id);

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

            await NotifyJobCompletedAsync(job);

            _logger.LogInformation("✅ Job atualizado com sucesso: {JobId}", job.JobId);

            return new JobStatusResponse
            {
                JobId = job.JobId,
                Status = job.Status,
                ImageUrls = job.ImageUrls,
                UpdatedAt = job.UpdatedAt
            };
        }

        public async Task<JobStatusResponse> ProcessarWebhookReplicateAsync(ReplicateWebhookRequest payload)
        {
            _logger.LogInformation("🔁 Processando webhook Replicate: JobId={JobId}", payload.Id);

            if (string.IsNullOrWhiteSpace(payload.Status) || payload.Status.ToLower() != "succeeded" || string.IsNullOrWhiteSpace(payload.Output))
                throw new ValidationAppException("Payload incompleto ou job ainda não finalizado.");

            var job = await _repository.GetByJobIdAsync(payload.Id);
            if (job == null)
                throw new ValidationAppException($"JobId '{payload.Id}' não encontrado no banco.");

            var imageUrl = await BaixarImagemReplicateAsync(payload.Output, payload.Id);

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

            await NotifyJobCompletedAsync(job);

            _logger.LogInformation("✅ Job Replicate finalizado com sucesso: {JobId}", job.JobId);

            return new JobStatusResponse
            {
                JobId = job.JobId,
                Status = job.Status,
                ImageUrls = job.ImageUrls,
                UpdatedAt = job.UpdatedAt
            };
        }

        private async Task<string> SalvarImagemAsync(string base64Data, string jobId)
        {
            try
            {
                var base64 = Regex.Replace(base64Data, @"^data:image/[a-zA-Z]+;base64,", string.Empty);
                var bytes = Convert.FromBase64String(base64);
                using var ms = new MemoryStream(bytes);
                var key = $"images/{jobId}.png";
                return await _storage.UploadAsync(ms, key, "image/png")
                    ?? throw new StorageUploadException(provider: "R2");
            }
            catch (StorageUploadException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao salvar imagem do job {JobId}", jobId);
                throw new StorageUploadException(provider: "R2");
            }
        }

        private async Task<string> BaixarImagemReplicateAsync(string imageUrl, string jobId)
        {
            try
            {
                using var client = new HttpClient();
                var bytes = await client.GetByteArrayAsync(imageUrl);
                using var ms = new MemoryStream(bytes);
                var key = $"images/{jobId}.png";
                return await _storage.UploadAsync(ms, key, "image/png")
                    ?? throw new StorageUploadException(provider: "R2");
            }
            catch (StorageUploadException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao baixar imagem do Replicate para o job {JobId}", jobId);
                throw new StorageUploadException(provider: "R2");
            }
        }

        private async Task NotifyJobCompletedAsync(ImageJob job)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(job.UserId))
                {
                    _logger.LogWarning("Job {JobId} não possui UserId associado para notificação SignalR.", job.JobId);
                    return;
                }

                await _hubContext.Clients.User(job.UserId)
                    .SendAsync("JobCompleted", new
                    {
                        JobId = job.JobId,
                        Status = job.Status,
                        ImageUrls = job.ImageUrls,
                        AspectRatio = job.AspectRatio,
                        CreatedAt = job.CreatedAt,
                        Prompt = job.Prompt
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao enviar notificação SignalR para o job {JobId}", job.JobId);
            }
        }
    }
}
