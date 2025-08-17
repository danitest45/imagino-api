using Imagino.Api.DTOs;
using Imagino.Api.Models;
using Imagino.Api.Services.ImageGeneration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Imagino.Api.Controllers;

/// <summary>
/// Controller para gerar imagens usando o Replicate.
/// </summary>
[ApiController]
[Route("api/replicate/jobs")]
[Authorize]

public class ReplicateJobsController(IReplicateJobsService replicateService) : ControllerBase
{
    private readonly IReplicateJobsService _replicateService = replicateService;

    /// <summary>
    /// Cria uma nova imagem via Replicate.
    /// </summary>
    /// <param name="request">Requisição contendo prompt e parâmetros.</param>
    /// <returns>Resultado com JobId e status da criação.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(RequestResult), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(RequestResult), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateReplicateJob([FromBody] ImageGenerationReplicateRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var result = await _replicateService.GenerateImageAsync(request, userId!);

        if (!result.Success)
            return BadRequest(result);

        return CreatedAtAction(nameof(GetJobById), new JobCreatedResponse { JobId = result.Content?.JobId }, result);
    }

    /// <summary>
    /// Consulta o status de um job específico no Replicate.
    /// </summary>
    /// <param name="jobId">ID do job.</param>
    /// <returns>Status do job.</returns>
    [HttpGet("{jobId}")]
    public async Task<IActionResult> GetJobById(string jobId)
    {
        //var result = await _replicateService.GetJobStatusAsync(jobId);

        //if (!result.Success)
        //    return BadRequest(result);

        return Ok();
    }
}
