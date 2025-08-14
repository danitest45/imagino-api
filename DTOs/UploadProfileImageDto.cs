using Microsoft.AspNetCore.Http;

namespace Imagino.Api.DTOs
{
    public class UploadProfileImageDto
    {
        public IFormFile File { get; set; } = default!;
    }
}
