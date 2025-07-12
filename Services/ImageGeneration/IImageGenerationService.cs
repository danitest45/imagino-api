using Imagino.Api.DTOs;
using Imagino.Api.Models;

namespace Imagino.Api.Services.ImageGeneration
{
    public interface IImageGenerationService
    {
        Task<RequestResult> GenerateImageAsync(ImageGenerationRequest request);
    }
}
