using Imagino.Api.DTOs;
using Imagino.Api.Models;

namespace Imagino.Api.Services.ImageGeneration
{
    public interface IJobsService
    {
        Task<RequestResult> GenerateImageAsync(ImageGenerationRunPodRequest request);
        Task<RequestResult> GetJobByIdAsync(string jobId);
    }
}
