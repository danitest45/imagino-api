using System;
using Imagino.Api.DTOs;
using Imagino.Api.Services.ImageGeneration;

namespace Imagino.Api.Services.ImageGeneration.Models
{
    public class Seedream4ReplicateModelRequestBuilder : IReplicateModelRequestBuilder
    {
        public string ModelKey => "seedream-4";

        public object BuildRequest(ImageGenerationReplicateRequest request, string? webhookUrl)
        {
            var aspectRatio = string.IsNullOrWhiteSpace(request.AspectRatio) ? "4:3" : request.AspectRatio;

            return new
            {
                input = new
                {
                    size = "2K",
                    width = 2048,
                    height = 2048,
                    prompt = request.Prompt,
                    max_images = 1,
                    image_input = Array.Empty<object>(),
                    aspect_ratio = aspectRatio,
                    enhance_prompt = true,
                    sequential_image_generation = "disabled"
                },
                webhook = webhookUrl,
                webhook_events_filter = new[] { "completed" }
            };
        }
    }
}
