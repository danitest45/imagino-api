using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Imagino.Api.DTOs;
using Imagino.Api.DTOs.Image;
using Imagino.Api.Errors;
using Imagino.Api.Services.Image;
using Imagino.Api.Services.ImageGeneration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Imagino.Api.Controllers.Image
{
    [ApiController]
    [Route("api/image/jobs")]
    [Authorize]
    public class ImageJobsController : ControllerBase
    {
        private readonly IImageJobCreationService _jobCreationService;
        private readonly IJobsService _jobsService;

        public ImageJobsController(IImageJobCreationService jobCreationService, IJobsService jobsService)
        {
            _jobCreationService = jobCreationService;
            _jobsService = jobsService;
        }

        [HttpPost]
        public async Task<ActionResult<JobCreatedResponse>> Create([FromBody] CreateImageJobRequest request)
        {
            var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ValidationAppException("Authenticated user not found");
            }

            request.Params ??= JsonDocument.Parse("{}");
            var result = await _jobCreationService.CreateJobAsync(request, userId);
            return CreatedAtAction(nameof(GetJobById), new { jobId = result.JobId }, result);
        }

        [HttpGet("{jobId}")]
        public async Task<ActionResult<JobStatusResponse>> GetJobById(string jobId)
        {
            var job = await _jobsService.GetJobByIdAsync(jobId);
            return Ok(job);
        }
    }
}
