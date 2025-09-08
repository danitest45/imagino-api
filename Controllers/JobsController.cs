using Imagino.Api.DTOs;
using Imagino.Api.Services.ImageGeneration;
using Imagino.Api.Repository;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using System.Security.Claims;
using System.Net.Http;
using System.IO;
using System;

namespace Imagino.Api.Controllers;

/// <summary>
/// Handles image generation requests by creating new jobs.
/// </summary>
[ApiController]
[Route("api/jobs")]
[Authorize]

public class JobsController(IJobsService imageService,
                            IImageJobRepository jobRepository,
                            IUserRepository userRepository) : ControllerBase
{
    private readonly IJobsService _imageService = imageService;
    private readonly IImageJobRepository _jobRepository = jobRepository;
    private readonly IUserRepository _userRepository = userRepository;

    /// <summary>
    /// Creates a new image generation job.
    /// </summary>
    /// <param name="request">The image generation request containing prompt and settings.</param>
    /// <returns>A result indicating success or failure, along with job data.</returns>
    /// <response code="201">Image generation job created successfully.</response>
    /// <response code="400">Invalid request or error while creating the job.</response>
    [HttpPost]
    [ProducesResponseType(typeof(JobCreatedResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateJob([FromBody] ImageGenerationRunPodRequest request)
    {
        var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                 ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized("User ID not found");

        var job = await _imageService.GenerateImageAsync(request, userId);

        return CreatedAtAction(nameof(GetJobById), new JobCreatedResponse{ JobId = job.JobId }, job);
    }

    /// <summary>
    /// Retrieves the status of an image generation job by JobId.
    /// </summary>
    /// <param name="jobId">The ID of the job to retrieve</param>
    /// <returns>Status and metadata of the job</returns>
    [HttpGet("{jobId}")]
    public async Task<IActionResult> GetJobById(string jobId)
    {
        var job = await _imageService.GetJobByIdAsync(jobId);

        return Ok(job);
    }

    [HttpGet("details/{jobId}")]
    public async Task<IActionResult> GetJobDetails(string jobId)
    {
        var job = await _jobRepository.GetByJobIdAsync(jobId);
        if (job == null)
            return NotFound();

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
            return NotFound();

        var imageUrl = job.ImageUrls.First();

        try
        {
            using var client = new HttpClient();
            var bytes = await client.GetByteArrayAsync(imageUrl);

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
    public async Task<IActionResult> GetLatestJobs()
    {
        var jobs = await _jobRepository.GetLatestAsync(12);
        var responseTasks = jobs.Select(async job =>
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
        return Ok(response);
    }
}
