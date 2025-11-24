using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Imagino.Api.DTOs;
using Imagino.Api.DTOs.Image;
using Imagino.Api.Errors;
using Imagino.Api.Models.Image;
using Imagino.Api.Repositories.Image;
using Imagino.Api.Services.Image;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Imagino.Api.Controllers.Admin.Image
{
    [ApiController]
    [Route("api/admin/image/models")]
    [Authorize]
    public class ImageModelsController : ControllerBase
    {
        private readonly IImageModelRepository _modelRepository;
        private readonly IImageModelProviderRepository _providerRepository;
        private readonly IPublicImageModelCacheService _publicModelCacheService;

        public ImageModelsController(IImageModelRepository modelRepository, IImageModelProviderRepository providerRepository, IPublicImageModelCacheService publicImageModelCacheService)
        {
            _modelRepository = modelRepository;
            _providerRepository = providerRepository;
            _publicModelCacheService = publicImageModelCacheService;
        }

        [HttpGet]
        public async Task<IActionResult> List([FromQuery] ImageModelStatus? status, [FromQuery] ImageModelVisibility? visibility, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            var safePage = Math.Max(1, page);
            var safePageSize = Math.Clamp(pageSize, 1, 200);
            var paged = await _modelRepository.GetPagedAsync(status, visibility, safePage, safePageSize);
            var result = paged.Items.Select(ToDto).ToList();
            return Ok(new PagedResult<ImageModelDto>
            {
                Items = result,
                Total = paged.Total
            });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var model = await _modelRepository.GetByIdAsync(id);
            if (model == null)
            {
                return NotFound();
            }

            return Ok(ToDto(model));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateImageModelDto dto)
        {
            await EnsureProviderExists(dto.ProviderId);

            var existing = await _modelRepository.GetBySlugAsync(dto.Slug);
            if (existing != null)
            {
                throw new ConflictAppException($"Model with slug '{dto.Slug}' already exists");
            }

            var model = new ImageModel
            {
                Slug = dto.Slug,
                DisplayName = dto.DisplayName,
                ProviderId = dto.ProviderId,
                Capabilities = MapCapabilities(dto.Capabilities),
                Visibility = dto.Visibility,
                Status = dto.Status,
                DefaultVersionId = dto.DefaultVersionId,
                Pricing = MapPricing(dto.Pricing),
                Tags = dto.Tags,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _modelRepository.InsertAsync(model);
            _publicModelCacheService.BumpVersion();
            return CreatedAtAction(nameof(GetById), new { id = model.Id }, ToDto(model));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] UpdateImageModelDto dto)
        {
            var model = await _modelRepository.GetByIdAsync(id);
            if (model == null)
            {
                return NotFound();
            }

            await EnsureProviderExists(dto.ProviderId);

            model.Slug = dto.Slug;
            model.DisplayName = dto.DisplayName;
            model.ProviderId = dto.ProviderId;
            model.Capabilities = MapCapabilities(dto.Capabilities);
            model.Visibility = dto.Visibility;
            model.Status = dto.Status;
            model.DefaultVersionId = dto.DefaultVersionId;
            model.Pricing = MapPricing(dto.Pricing);
            model.Tags = dto.Tags;
            model.UpdatedAt = DateTime.UtcNow;

            await _modelRepository.UpdateAsync(model);
            _publicModelCacheService.BumpVersion();
            return Ok(ToDto(model));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            await _modelRepository.DeleteAsync(id);
            _publicModelCacheService.BumpVersion();
            return NoContent();
        }

        [HttpPost("{id}/default-version")]
        public async Task<IActionResult> SetDefaultVersion(string id, [FromBody] string versionId)
        {
            var model = await _modelRepository.GetByIdAsync(id);
            if (model == null)
            {
                return NotFound();
            }

            await _modelRepository.SetDefaultVersionAsync(id, versionId);
            model.DefaultVersionId = versionId;
            model.UpdatedAt = DateTime.UtcNow;
            _publicModelCacheService.BumpVersion();
            return Ok(ToDto(model));
        }

        private async Task EnsureProviderExists(string providerId)
        {
            var provider = await _providerRepository.GetByIdAsync(providerId);
            if (provider == null)
            {
                throw new ValidationAppException($"Provider '{providerId}' not found");
            }
        }

        private static ImageModelCapabilities MapCapabilities(ImageModelCapabilitiesDto dto) => new()
        {
            Image = dto.Image,
            Inpaint = dto.Inpaint,
            Upscale = dto.Upscale
        };

        private static ImageModelPricing MapPricing(ImageModelPricingDto dto) => new()
        {
            CreditsPerImage = dto.CreditsPerImage,
            MinCredits = dto.MinCredits
        };

        private static ImageModelDto ToDto(ImageModel model) => new()
        {
            Id = model.Id,
            Slug = model.Slug,
            DisplayName = model.DisplayName,
            ProviderId = model.ProviderId,
            Capabilities = new ImageModelCapabilitiesDto
            {
                Image = model.Capabilities.Image,
                Inpaint = model.Capabilities.Inpaint,
                Upscale = model.Capabilities.Upscale
            },
            Visibility = model.Visibility,
            Status = model.Status,
            DefaultVersionId = model.DefaultVersionId,
            Pricing = new ImageModelPricingDto
            {
                CreditsPerImage = model.Pricing.CreditsPerImage,
                MinCredits = model.Pricing.MinCredits
            },
            Tags = model.Tags,
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt
        };
    }
}
