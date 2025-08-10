using Imagino.Api.DTOs;
using Imagino.Api.Models;
using Imagino.Api.Repository;
using Imagino.Api.Settings;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Imagino.Api.Services.ImageGeneration
{
    public class ReplicateJobsService(HttpClient httpClient, IOptions<ReplicateSettings> settings, IImageJobRepository jobRepository) : IReplicateJobsService
    {
        private readonly HttpClient _httpClient = httpClient;
        private readonly ReplicateSettings _settings = settings.Value;
        private readonly IImageJobRepository _jobRepository = jobRepository;

        public async Task<RequestResult> GenerateImageAsync(ImageGenerationReplicateRequest request, string userId)
        {
            var result = new RequestResult();

            try
            {
                (int steps, double guidance) = request.QualityLevel switch
                {
                    1 => (10, 2.0),
                    2 => (15, 2.5),
                    3 => (25, 3.0),
                    4 => (35, 4.0),
                    5 => (50, 5.0),
                    _ => (25, 3.0)
                };
                var replicateRequest = new
                {
                    input = new
                    {
                        steps = steps,
                        width = 1024,
                        height = 1024,
                        prompt = request.Prompt,
                        guidance = guidance,
                        interval = 2,
                        aspect_ratio = request.AspectRatio,
                        output_format = "png",
                        output_quality = 100,
                        safety_tolerance = 2,
                        prompt_upsampling = false
                    },
                    webhook = _settings.WebhookUrl,
                    webhook_events_filter = new[] { "completed" }
                };

                var json = JsonSerializer.Serialize(replicateRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", _settings.ApiKey);

                var response = await _httpClient.PostAsync(_settings.ModelUrl, content);

                if (!response.IsSuccessStatusCode)
                {
                    result.AddError($"Replicate API returned error: {response.StatusCode}");
                    return result;
                }

                var body = await response.Content.ReadAsStringAsync();
                var replicateResponse = JsonSerializer.Deserialize<ReplicateRawResponse>(body);

                var imageJob = new ImageJob
                {
                    Prompt = request.Prompt,
                    JobId = replicateResponse!.id,
                    Status = replicateResponse.status.ToLower(),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    UserId = userId
                };

                await _jobRepository.InsertAsync(imageJob);

                result.Content = new
                {
                    imageJob.JobId,
                    imageJob.Status,
                };

                return result;
            }
            catch (Exception ex)
            {
                result.AddError("Unexpected error with Replicate.");
                Console.Error.WriteLine(ex);
            }

            return result;
        }

    }

}
