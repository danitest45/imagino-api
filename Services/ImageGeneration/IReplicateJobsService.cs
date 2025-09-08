using Imagino.Api.DTOs;

namespace Imagino.Api.Services.ImageGeneration
{
    public interface IReplicateJobsService
    {
        Task<JobCreatedResponse> GenerateImageAsync(ImageGenerationReplicateRequest request, string userId);
    }
}