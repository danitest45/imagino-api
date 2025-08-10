// Controllers/HistoryController.cs
using Imagino.Api.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using System.Security.Claims;

[ApiController]
[Route("api/[controller]")]
public class HistoryController : ControllerBase
{
    private readonly IImageJobRepository _repo;

    public HistoryController(IImageJobRepository repo)
    {
        _repo = repo;
    }

    [HttpGet]
    public async Task<IActionResult> GetUserHistory()
    {
        var userId =
        User.FindFirstValue(JwtRegisteredClaimNames.Sub) ??
        User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
            return Unauthorized("UserId not found in token.");

        var jobs = await _repo.GetByUserIdAsync(userId);
        return Ok(jobs.OrderByDescending(j => j.CreatedAt));
    }
}
