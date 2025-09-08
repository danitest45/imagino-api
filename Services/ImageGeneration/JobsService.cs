using Imagino.Api.DTOs;
using Imagino.Api.Errors;
using Imagino.Api.Models;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text;
using System.Net.Http.Headers;
using Imagino.Api.Settings;
using Imagino.Api.Repository;

namespace Imagino.Api.Services.ImageGeneration
{
    public class JobsService(HttpClient httpClient,
        IOptions<ImageGeneratorSettings> settings,
        IImageJobRepository jobRepository,
        IUserRepository userRepository) : IJobsService
    {
        private readonly HttpClient _httpClient = httpClient;
        private readonly ImageGeneratorSettings _settings = settings.Value;
        private readonly IImageJobRepository _jobRepository = jobRepository;
        private readonly IUserRepository _userRepository = userRepository;

        public async Task<JobCreatedResponse> GenerateImageAsync(ImageGenerationRunPodRequest request, string userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                throw new ValidationAppException("User not found");

            if (user.Credits < _settings.ImageCost)
                throw new InsufficientCreditsException(user.Credits, _settings.ImageCost);

            var payload = new
            {
                input = new
                {
                    prompt = request.Prompt,
                    negative_prompt = request.NegativePrompt,
                    steps = request.Steps,
                    cfg_scale = request.CfgScale,
                    width = request.Width,
                    height = request.Height,
                    sampler_name = request.SamplerName
                },
                webhook = _settings.WebhookUrl
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _settings.RunPodApiKey);

            var response = await _httpClient.PostAsync(_settings.RunPodApiUrl, content);

            if (!response.IsSuccessStatusCode)
                throw new UpstreamServiceException("RunPod", response.StatusCode.ToString(), (int)response.StatusCode);

            var responseBody = await response.Content.ReadAsStringAsync();
            var runpodRaw = JsonSerializer.Deserialize<RunPodContentResponse>(responseBody);

            var imageJob = new ImageJob
            {
                Prompt = request.Prompt,
                JobId = runpodRaw!.id,
                Status = runpodRaw.status.ToLower(),
                UserId = userId,
                AspectRatio = CalculateAspectRatio(request.Width, request.Height),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                ImageUrls = new List<string>(),
                TokenConsumed = false
            };

            await _jobRepository.InsertAsync(imageJob);

            return new JobCreatedResponse
            {
                JobId = imageJob.JobId,
                Status = imageJob.Status,
                CreatedAt = imageJob.CreatedAt
            };
        }

        public async Task<JobStatusResponse> GetJobByIdAsync(string jobId)
        {
            var job = await _jobRepository.GetByJobIdAsync(jobId);
            if (job == null)
                throw new ValidationAppException($"Job with ID '{jobId}' not found");

            return new JobStatusResponse
            {
                JobId = job.JobId,
                Status = job.Status,
                ImageUrls = job.ImageUrls,
                UpdatedAt = job.UpdatedAt
            };
        }

        private static string CalculateAspectRatio(int width, int height)
        {
            static int Gcd(int a, int b) => b == 0 ? a : Gcd(b, a % b);
            var gcd = Gcd(width, height);
            return $"{width / gcd}:{height / gcd}";
        }
    }

}
