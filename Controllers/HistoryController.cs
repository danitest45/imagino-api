// Controllers/HistoryController.cs
using Imagino.Api.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
    [Authorize] // garante que só usuários autenticados acessem
    public async Task<IActionResult> GetHistory()
    {
        var userId = User.FindFirst("sub")?.Value;
        if (userId == null) return Unauthorized();

        var jobs = await _repo.GetByJobIdAsync(userId);
        return Ok(jobs);
    }
}
