using Imagino.Api.DTOs;
using Imagino.Api.Models;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text;
using System.Net.Http.Headers;
using Imagino.Api.Settings;

namespace Imagino.Api.Services.ImageGeneration
{
    public class ImageGenerationService(HttpClient httpClient, IOptions<ImageGeneratorSettings> settings) : IImageGenerationService
    {
        private readonly HttpClient _httpClient = httpClient;
        private readonly ImageGeneratorSettings _settings = settings.Value;

        public async Task<RequestResult> GenerateImageAsync(ImageGenerationRequest request)
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
                        sampler_name = "Euler"
                    }
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _settings.RunPodApiKey);

                var response = await _httpClient.PostAsync(_settings.RunPodApiUrl, content);

                if (!response.IsSuccessStatusCode)
                {
                    result.AddError($"API returned error: {response.StatusCode}");
                    return result;
                }

                var responseBody = await response.Content.ReadAsStringAsync();
                result.Content = responseBody;
            }
            catch (Exception ex)
            {
                result.AddError($"Exception: {ex.Message}");
            }

            return result;
        }
    }
}
