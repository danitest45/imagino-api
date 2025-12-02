using System.Security.Claims;
using Imagino.Api.DTOs;
using Imagino.Api.DTOs.Video;
using Imagino.Api.Errors;
using Imagino.Api.Repository;
using Imagino.Api.Services.Video;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Imagino.Api.Controllers
{
    [ApiController]
    [Route("api/video/jobs")]
    [Authorize]
    public class VideoController : ControllerBase
    {
        private readonly IVideoJobCreationService _jobCreationService;
        private readonly IVideoJobRepository _jobRepository;

        public VideoController(
            IVideoJobCreationService jobCreationService,
            IVideoJobRepository jobRepository)
        {
            _jobCreationService = jobCreationService;
            _jobRepository = jobRepository;
        }

        [HttpPost]
        public async Task<ActionResult<JobCreatedResponse>> Create([FromBody] CreateVideoJobRequest request)
        {
            var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ValidationAppException("Authenticated user not found");
            }

            var result = await _jobCreationService.CreateJobAsync(request, userId);
            return CreatedAtAction(nameof(GetJobById), new { id = result.JobId }, result);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<VideoJobStatusResponse>> GetJobById(string id)
        {
            var job = await _jobRepository.GetByJobIdAsync(id);
            if (job == null)
            {
                return NotFound();
            }

            var response = new VideoJobStatusResponse
            {
                JobId = job.JobId,
                Status = job.Status.ToString(),
                VideoUrl = job.VideoUrl,
                UpdatedAt = job.UpdatedAt,
                DurationSeconds = job.DurationSeconds,
                ErrorMessage = job.ErrorMessage
            };

            return Ok(response);
        }
    }
}
