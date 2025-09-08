using Imagino.Api.DTOs;

namespace Imagino.Api.Services.ImageGeneration
{
    public interface IJobsService
    {
        Task<JobCreatedResponse> GenerateImageAsync(ImageGenerationRunPodRequest request, string userId);
        Task<JobStatusResponse> GetJobByIdAsync(string jobId);
    }
}
