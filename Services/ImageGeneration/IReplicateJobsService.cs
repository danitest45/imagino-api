using Imagino.Api.DTOs;
using Imagino.Api.Models;

namespace Imagino.Api.Services.ImageGeneration
{
    public interface IReplicateJobsService
    {
        Task<RequestResult> GenerateImageAsync(ImageGenerationReplicateRequest request);
    }
}