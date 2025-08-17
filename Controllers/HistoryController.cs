// Controllers/HistoryController.cs
using Imagino.Api.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using System.Security.Claims;

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
    public async Task<IActionResult> Get()
    {
        var userId =
           User.FindFirstValue(JwtRegisteredClaimNames.Sub) ??
           User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
            return Unauthorized("User ID not found");

        var jobs = await _repo.GetByUserIdAsync(userId);
        return Ok(jobs.OrderByDescending(j => j.CreatedAt));
    }
}
