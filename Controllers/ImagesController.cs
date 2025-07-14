using Imagino.Api.DTOs;
using Imagino.Api.Services.ImageGeneration;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Imagino.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ImagesController : ControllerBase
    {
        private readonly IImageGenerationService _imageService;

        public ImagesController(IImageGenerationService imageService)
        {
            _imageService = imageService;
        }

        [HttpPost("generate")]
        public async Task<IActionResult> GenerateImage([FromBody] ImageGenerationRequest request)
        {
            var result = await _imageService.GenerateImageAsync(request);

            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }
    }
}
