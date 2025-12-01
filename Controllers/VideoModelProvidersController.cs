using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Imagino.Api.DTOs.Video;
using Imagino.Api.Models.Video;
using Imagino.Api.Services.Video;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Imagino.Api.Controllers
{
    [ApiController]
    [Route("api/video/providers")]
    public class VideoModelProvidersController : ControllerBase
    {
        private readonly IVideoModelProviderService _providerService;

        public VideoModelProvidersController(IVideoModelProviderService providerService)
        {
            _providerService = providerService;
        }

        [HttpGet]
        public async Task<ActionResult<List<VideoModelProviderDto>>> List()
        {
            var providers = await _providerService.ListAsync();
            var dtos = providers.Select(ToDto).ToList();
            return Ok(dtos);
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult<VideoModelProviderDto>> Create([FromBody] CreateVideoModelProviderDto dto)
        {
            var provider = new VideoModelProvider
            {
                Name = dto.Name,
                ProviderType = dto.ProviderType,
                Config = dto.Config
            };

            var created = await _providerService.CreateAsync(provider);
            return CreatedAtAction(nameof(List), new { id = created.Id }, ToDto(created));
        }

        [HttpPut("{providerId}")]
        [Authorize]
        public async Task<ActionResult<VideoModelProviderDto>> Update(string providerId, [FromBody] UpdateVideoModelProviderDto dto)
        {
            var provider = new VideoModelProvider
            {
                Name = dto.Name,
                ProviderType = dto.ProviderType,
                Config = dto.Config
            };

            var updated = await _providerService.UpdateAsync(providerId, provider);
            if (updated == null)
            {
                return NotFound();
            }

            return Ok(ToDto(updated));
        }

        private static VideoModelProviderDto ToDto(VideoModelProvider provider) => new()
        {
            Id = provider.Id,
            Name = provider.Name,
            ProviderType = provider.ProviderType,
            Config = provider.Config,
            CreatedAt = provider.CreatedAt,
            UpdatedAt = provider.UpdatedAt
        };
    }
}
