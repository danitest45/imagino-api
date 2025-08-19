using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Imagino.Api.Services.Storage;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace Imagino.Api.Controllers
{
    [ApiController]
    [Route("api/files/{*key}")]
    public class FilesController : ControllerBase
    {
        private readonly IStorageService _storageService;
        private const string DownloadCorsPolicy = "AllowDownload";

        public FilesController(IStorageService storageService)
        {
            _storageService = storageService;
        }

        [HttpGet("download-url")]
        [EnableCors(DownloadCorsPolicy)]
        public async Task<IActionResult> GetDownloadUrl([FromRoute] string key, [FromQuery] string? prompt)
        {
            var fileName = BuildFileName(key, prompt);
            var url = await _storageService.GetDownloadUrlAsync(key, fileName);
            return Ok(new { url });
        }

        private static string BuildFileName(string key, string? prompt)
        {
            if (!string.IsNullOrWhiteSpace(prompt))
            {
                var words = prompt.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Take(6);
                var joined = string.Join(" ", words);
                var baseName = Slugify(joined);
                return $"{baseName}.png";
            }

            var fileName = Path.GetFileName(key);
            var changed = Path.ChangeExtension(fileName, ".png");
            return changed ?? "image.png";
        }

        private static string Slugify(string phrase)
        {
            var lower = phrase.ToLowerInvariant();
            lower = Regex.Replace(lower, @"[^a-z0-9\s-]", "");
            lower = Regex.Replace(lower, @"\s+", " ").Trim();
            return lower.Replace(' ', '-');
        }
    }
}

