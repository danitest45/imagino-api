using System;
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
    [Route("api/video/models")]
    public class VideoModelsController : ControllerBase
    {
        private readonly IVideoModelService _modelService;

        public VideoModelsController(IVideoModelService modelService)
        {
            _modelService = modelService;
        }

        [HttpGet]
        public async Task<ActionResult<List<VideoModelDto>>> List([FromQuery] VideoModelStatus? status, [FromQuery] VideoModelVisibility? visibility)
        {
            var models = await _modelService.ListAsync(status, visibility);
            var result = models.Select(ToDto).ToList();
            return Ok(result);
        }

        [HttpGet("{slug}")]
        public async Task<ActionResult<VideoModelDto>> GetBySlug(string slug)
        {
            var model = await _modelService.GetBySlugAsync(slug);
            if (model == null)
            {
                return NotFound();
            }

            return Ok(ToDto(model));
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult<VideoModelDto>> Create([FromBody] CreateVideoModelDto dto)
        {
            var model = new VideoModel
            {
                Slug = dto.Slug,
                DisplayName = dto.DisplayName,
                ProviderId = dto.ProviderId,
                Capabilities = new VideoModelCapabilities
                {
                    Video = dto.Capabilities.Video
                },
                Visibility = dto.Visibility,
                Status = dto.Status,
                DefaultVersionId = dto.DefaultVersionId,
                Pricing = new VideoModelPricing
                {
                    CreditsPerSecond = dto.Pricing.CreditsPerSecond,
                    CreditsPerVideo = dto.Pricing.CreditsPerVideo,
                    MinCredits = dto.Pricing.MinCredits
                },
                Tags = dto.Tags
            };

            var created = await _modelService.CreateAsync(model);
            return CreatedAtAction(nameof(GetBySlug), new { slug = created.Slug }, ToDto(created));
        }

        [HttpPut("{id}")]
        [Authorize]
        public async Task<ActionResult<VideoModelDto>> Update(string id, [FromBody] UpdateVideoModelDto dto)
        {
            var updatedModel = new VideoModel
            {
                Slug = dto.Slug,
                DisplayName = dto.DisplayName,
                ProviderId = dto.ProviderId,
                Capabilities = new VideoModelCapabilities
                {
                    Video = dto.Capabilities.Video
                },
                Visibility = dto.Visibility,
                Status = dto.Status,
                DefaultVersionId = dto.DefaultVersionId,
                Pricing = new VideoModelPricing
                {
                    CreditsPerSecond = dto.Pricing.CreditsPerSecond,
                    CreditsPerVideo = dto.Pricing.CreditsPerVideo,
                    MinCredits = dto.Pricing.MinCredits
                },
                Tags = dto.Tags
            };

            var result = await _modelService.UpdateAsync(id, updatedModel);
            if (result == null)
            {
                return NotFound();
            }

            return Ok(ToDto(result));
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> Delete(string id)
        {
            await _modelService.DeleteAsync(id);
            return NoContent();
        }

        private static VideoModelDto ToDto(VideoModel model) => new()
        {
            Id = model.Id,
            Slug = model.Slug,
            DisplayName = model.DisplayName,
            ProviderId = model.ProviderId,
            Capabilities = new VideoModelCapabilitiesDto
            {
                Video = model.Capabilities.Video
            },
            Visibility = model.Visibility,
            Status = model.Status,
            DefaultVersionId = model.DefaultVersionId,
            Pricing = new VideoModelPricingDto
            {
                CreditsPerSecond = model.Pricing.CreditsPerSecond,
                CreditsPerVideo = model.Pricing.CreditsPerVideo,
                MinCredits = model.Pricing.MinCredits
            },
            Tags = model.Tags,
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt
        };
    }
}
