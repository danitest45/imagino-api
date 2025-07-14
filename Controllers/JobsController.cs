using Imagino.Api.DTOs;
using Imagino.Api.Models;
using Imagino.Api.Services.ImageGeneration;
using Microsoft.AspNetCore.Mvc;

namespace Imagino.Api.Controllers;

/// <summary>
/// Handles image generation requests by creating new jobs.
/// </summary>
[ApiController]
[Route("api/jobs")]
public class JobsController(IJobsService imageService) : ControllerBase
{
    private readonly IJobsService _imageService = imageService;

    /// <summary>
    /// Creates a new image generation job.
    /// </summary>
    /// <param name="request">The image generation request containing prompt and settings.</param>
    /// <returns>A result indicating success or failure, along with job data.</returns>
    /// <response code="201">Image generation job created successfully.</response>
    /// <response code="400">Invalid request or error while creating the job.</response>
    [HttpPost]
    [ProducesResponseType(typeof(RequestResult), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(RequestResult), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateJob([FromBody] ImageGenerationRequest request)
    {
        var result = await _imageService.GenerateImageAsync(request);

        if (!result.Success)
            return BadRequest(result);

        return CreatedAtAction(nameof(GetJobById), new JobCreatedResponse{ JobId = result.Content?.JobId }, result);
    }

    /// <summary>
    /// Placeholder for retrieving job by ID (to be implemented).
    /// </summary>
    /// <param name="jobId">The job ID.</param>
    /// <returns>A dummy 501 response.</returns>
    [HttpGet("{jobId}")]
    public IActionResult GetJobById(string jobId)
    {
        return StatusCode(501, "Not implemented yet.");
    }
}
