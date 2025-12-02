using Imagino.Api.DTOs;
using Imagino.Api.DTOs.Video;

namespace Imagino.Api.Services.Video
{
    public interface IVideoJobCreationService
    {
        Task<JobCreatedResponse> CreateJobAsync(CreateVideoJobRequest request, string userId);
    }
}
