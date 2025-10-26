using Imagino.Api.DTOs;
using Imagino.Api.Services.ImageGeneration;

namespace Imagino.Api.Services.ImageGeneration.Models
{
    public class FluxReplicateModelRequestBuilder : IReplicateModelRequestBuilder
    {
        public string ModelKey => "flux";

        public object BuildRequest(ImageGenerationReplicateRequest request, string? webhookUrl)
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

            return new
            {
                input = new
                {
                    steps,
                    width = 1024,
                    height = 1024,
                    prompt = request.Prompt,
                    guidance,
                    interval = 2,
                    aspect_ratio = request.AspectRatio,
                    output_format = "png",
                    output_quality = 100,
                    safety_tolerance = 2,
                    prompt_upsampling = false
                },
                webhook = webhookUrl,
                webhook_events_filter = new[] { "completed" }
            };
        }
    }
}
