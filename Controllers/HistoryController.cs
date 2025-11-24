// Controllers/HistoryController.cs
using Imagino.Api.DTOs;
using Imagino.Api.Models;
using Imagino.Api.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using System.Security.Claims;
using System;
using System.Linq;

[ApiController]
[Route("api/history")]
[Authorize]
public class HistoryController : ControllerBase
{
    private readonly IImageJobRepository _repo;

    public HistoryController(IImageJobRepository repo)
    {
        _repo = repo;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId =
           User.FindFirstValue(JwtRegisteredClaimNames.Sub) ??
           User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
            return Unauthorized("User ID not found");

        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 100);

        var jobs = await _repo.GetByUserIdAsync(userId, safePage, safePageSize);
        return Ok(new PagedResult<ImageJob>
        {
            Items = jobs.Items.OrderByDescending(j => j.CreatedAt),
            Total = jobs.Total
        });
    }
}
