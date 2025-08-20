using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Imagino.Api.Services.Storage;

namespace Imagino.Api.Controllers;

[ApiController]
[Route("api/files")]
public class FilesController : ControllerBase
{
    private readonly IStorageService _storage;
    public FilesController(IStorageService storage) => _storage = storage;

    // GET /api/files/download-url?key=images/abc.png&prompt=...
    [HttpGet("download-url")]
    public IActionResult GetDownloadUrl([FromQuery] string key, [FromQuery] string? prompt = null)
    {
        key = Uri.UnescapeDataString(key);
        var fileName = BuildFileNameFromPrompt(prompt, Path.GetFileName(key));
        var url = _storage.GetPresignedDownloadUrl(key, fileName, "image/png");
        return Ok(new { url });
    }

    private static string BuildFileNameFromPrompt(string? prompt, string fallbackBaseName)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return EnsurePng(fallbackBaseName);

        var words = Regex.Matches(prompt.ToLowerInvariant(), "[a-z0-9]+")
                         .Select(m => m.Value)
                         .Take(6);
        var slug = string.Join("-", words).Trim('-');
        if (string.IsNullOrEmpty(slug)) slug = "imagem";
        return $"{slug}.png";
    }

    private static string EnsurePng(string name)
    {
        var baseName = Path.GetFileNameWithoutExtension(name);
        return $"{(string.IsNullOrWhiteSpace(baseName) ? "imagem" : baseName)}.png";
    }
}
