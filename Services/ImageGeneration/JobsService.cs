using Imagino.Api.DTOs;
using Imagino.Api.Models;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text;
using System.Net.Http.Headers;
using Imagino.Api.Settings;
using Imagino.Api.Repository;
using Microsoft.AspNetCore.Mvc;

namespace Imagino.Api.Services.ImageGeneration
{
    public class JobsService(HttpClient httpClient,
        IOptions<ImageGeneratorSettings> settings,
        IImageJobRepository jobRepository) : IJobsService
    {
        private readonly HttpClient _httpClient = httpClient;
        private readonly ImageGeneratorSettings _settings = settings.Value;
        private readonly IImageJobRepository _jobRepository = jobRepository;

        public async Task<RequestResult> GenerateImageAsync(ImageGenerationRunPodRequest request)
        {
            var result = new RequestResult();

            try
            {
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
                {
                    result.AddError($"RunPod API returned error: {response.StatusCode}");
                    return result;
                }

                var responseBody = await response.Content.ReadAsStringAsync();
                var runpodRaw = JsonSerializer.Deserialize<RunPodContentResponse>(responseBody);

                var imageJob = new ImageJob
                {
                    Prompt = request.Prompt,
                    JobId = runpodRaw!.id,
                    Status = runpodRaw.status.ToLower(),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    ImageUrl = null
                };

                await _jobRepository.InsertAsync(imageJob);

                result.Content = new JobCreatedResponse
                {
                    JobId = imageJob.JobId,
                    Status = imageJob.Status,
                    CreatedAt = imageJob.CreatedAt
                };
            }
            catch (Exception ex)
            {
                result.AddError("Unexpected error during image generation.");
                Console.Error.WriteLine(ex);
            }

            return result;
        }
        public async Task<RequestResult> GetJobByIdAsync(string jobId)
        {
            var result = new RequestResult();

            var job = await _jobRepository.GetByJobIdAsync(jobId);
            if (job == null)
            {
                result.AddError($"Job with ID '{jobId}' not found.");
                return result;
            }

            result.Content = new JobStatusResponse
            {
                JobId = job.JobId,
                Status = job.Status,
                ImageUrl = job.ImageUrl,
                UpdatedAt = job.UpdatedAt
            };

            return result;
        }
    }

}
