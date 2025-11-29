using System;
using System.Linq;
using System.Threading.Tasks;
using Imagino.Api.DTOs.Image;
using Imagino.Api.Errors;
using Imagino.Api.Models.Image;
using Imagino.Api.Repositories.Image;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Imagino.Api.Controllers.Admin.Image
{
    [ApiController]
    [Route("api/admin/image/providers")]
    [Authorize]
    public class ImageModelProvidersController : ControllerBase
    {
        private readonly IImageModelProviderRepository _providerRepository;

        public ImageModelProvidersController(IImageModelProviderRepository providerRepository)
        {
            _providerRepository = providerRepository;
        }

        [HttpGet]
        public async Task<IActionResult> List([FromQuery] string? status)
        {
            var providers = await _providerRepository.GetAsync(status);
            var result = providers.Select(ToDto).ToList();
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var provider = await _providerRepository.GetByIdAsync(id);
            if (provider == null)
            {
                return NotFound();
            }

            return Ok(ToDto(provider));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateImageModelProviderDto dto)
        {
            var existing = await _providerRepository.GetByNameAsync(dto.Name);
            if (existing != null)
            {
                throw new ConflictAppException($"Provider with name '{dto.Name}' already exists");
            }

            var provider = new ImageModelProvider
            {
                Name = dto.Name,
                Status = dto.Status,
                ProviderType = dto.ProviderType,
                Notes = dto.Notes,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _providerRepository.InsertAsync(provider);
            return CreatedAtAction(nameof(GetById), new { id = provider.Id }, ToDto(provider));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] UpdateImageModelProviderDto dto)
        {
            var provider = await _providerRepository.GetByIdAsync(id);
            if (provider == null)
            {
                return NotFound();
            }

            if (dto.Name is not null)
            {
                provider.Name = dto.Name;
            }

            if (dto.Status is not null)
            {
                provider.Status = dto.Status;
            }

            if (dto.ProviderType.HasValue)
            {
                provider.ProviderType = dto.ProviderType.Value;
            }

            if (dto.Notes is not null)
            {
                provider.Notes = dto.Notes;
            }

            provider.UpdatedAt = DateTime.UtcNow;

            await _providerRepository.UpdateAsync(provider);
            return Ok(ToDto(provider));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            await _providerRepository.DeleteAsync(id);
            return NoContent();
        }

        private static ImageModelProviderDto ToDto(ImageModelProvider provider) => new()
        {
            Id = provider.Id,
            Name = provider.Name,
            Status = provider.Status,
            ProviderType = provider.ProviderType,
            Notes = provider.Notes,
            CreatedAt = provider.CreatedAt,
            UpdatedAt = provider.UpdatedAt
        };
    }
}
