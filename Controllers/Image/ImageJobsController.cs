using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using Imagino.Api.DTOs;
using Imagino.Api.DTOs.Image;
using Imagino.Api.Errors;
using Imagino.Api.Services.Image;
using Imagino.Api.Repository;
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
        private readonly IImageJobRepository _jobRepository;
        private readonly IUserRepository _userRepository;
        private readonly HttpClient _httpClient;

        public ImageJobsController(
            IImageJobCreationService jobCreationService,
            IImageJobRepository jobRepository,
            IUserRepository userRepository,
            HttpClient httpClient)
        {
            _jobCreationService = jobCreationService;
            _jobRepository = jobRepository;
            _userRepository = userRepository;
            _httpClient = httpClient;
        }

        [HttpPost]
        public async Task<ActionResult<JobCreatedResponse>> Create([FromBody] CreateImageJobRequest request)
        {
            var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ValidationAppException("Authenticated user not found");
            }

            var result = await _jobCreationService.CreateJobAsync(request, userId);
            return CreatedAtAction(nameof(GetJobById), new { jobId = result.JobId }, result);
        }

        [HttpGet("{jobId}")]
        public async Task<ActionResult<JobStatusResponse>> GetJobById(string jobId)
        {
            var job = await _jobRepository.GetByJobIdAsync(jobId);
            if (job == null)
            {
                return NotFound();
            }

            var response = new JobStatusResponse
            {
                JobId = job.JobId,
                Status = job.Status,
                ImageUrls = job.ImageUrls,
                UpdatedAt = job.UpdatedAt
            };

            return Ok(response);
        }

        [HttpGet("details/{jobId}")]
        public async Task<IActionResult> GetJobDetails(string jobId)
        {
            var job = await _jobRepository.GetByJobIdAsync(jobId);
            if (job == null)
            {
                return NotFound();
            }

            var user = string.IsNullOrEmpty(job.UserId)
                ? null
                : await _userRepository.GetByIdAsync(job.UserId);

            var response = new
            {
                ImageUrl = job.ImageUrls.FirstOrDefault(),
                job.Prompt,
                Username = user?.Username,
                job.CreatedAt,
                AspectRatio = job.AspectRatio
            };

            return Ok(response);
        }

        [HttpGet("{jobId}/download")]
        [AllowAnonymous]
        public async Task<IActionResult> DownloadImage(string jobId)
        {
            var job = await _jobRepository.GetByJobIdAsync(jobId);
            if (job == null || job.ImageUrls.Count == 0)
            {
                return NotFound();
            }

            var imageUrl = job.ImageUrls.First();

            try
            {
                var bytes = await _httpClient.GetByteArrayAsync(imageUrl);

                var extension = Path.GetExtension(new Uri(imageUrl).AbsolutePath).ToLowerInvariant();
                var contentType = extension switch
                {
                    ".png" => "image/png",
                    ".jpg" => "image/jpeg",
                    ".jpeg" => "image/jpeg",
                    ".gif" => "image/gif",
                    _ => "application/octet-stream"
                };

                var promptWords = (job.Prompt ?? string.Empty)
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var promptPrefix = string.Join("-", promptWords.Take(6));
                var invalidChars = Path.GetInvalidFileNameChars();
                var safePrompt = new string(promptPrefix.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
                var fileName = $"{safePrompt}{extension}";
                return File(bytes, contentType, fileName);
            }
            catch (Exception)
            {
                return NotFound();
            }
        }

        [HttpGet("latest")]
        [AllowAnonymous]
        public async Task<IActionResult> GetLatestJobs([FromQuery] int page = 1, [FromQuery] int pageSize = 12)
        {
            var safePage = Math.Max(1, page);
            var safePageSize = Math.Clamp(pageSize, 1, 50);

            var jobs = await _jobRepository.GetLatestAsync(safePage, safePageSize);
            var responseTasks = jobs.Items.Select(async job =>
            {
                var user = string.IsNullOrEmpty(job.UserId)
                    ? null
                    : await _userRepository.GetByIdAsync(job.UserId);

                return new
                {
                    Id = job.Id,
                    ImageUrl = job.ImageUrls.FirstOrDefault(),
                    job.Prompt,
                    Username = user?.Username,
                    job.CreatedAt,
                    AspectRatio = job.AspectRatio
                };
            });

            var response = await Task.WhenAll(responseTasks);
            return Ok(new PagedResult<object>
            {
                Items = response,
                Total = jobs.Total
            });
        }
    }
}
