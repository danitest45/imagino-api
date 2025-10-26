using Imagino.Api.DTOs.Image;
using Imagino.Api.DTOs;

namespace Imagino.Api.Services.Image
{
    public interface IImageJobCreationService
    {
        Task<JobCreatedResponse> CreateJobAsync(CreateImageJobRequest request, string userId);
    }
}
