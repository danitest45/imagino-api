using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Imagino.Api.Models.Image;
using Imagino.Api.Repositories.Image;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Imagino.Api.Controllers.Image
{
    [ApiController]
    [Route("api/image/models")]
    public class ImageModelsController : ControllerBase
    {
        private readonly IImageModelRepository _modelRepository;
        private readonly IImageModelVersionRepository _versionRepository;
        private readonly IImageModelPresetRepository _presetRepository;

        public ImageModelsController(
            IImageModelRepository modelRepository,
            IImageModelVersionRepository versionRepository,
            IImageModelPresetRepository presetRepository)
        {
            _modelRepository = modelRepository;
            _versionRepository = versionRepository;
            _presetRepository = presetRepository;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> List(
            [FromQuery] ImageModelVisibility? visibility,
            [FromQuery] ImageModelStatus? status,
            [FromQuery(Name = "include")] string? include)
        {
            var includeSet = (include ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => part.ToLowerInvariant())
                .ToHashSet();

            var models = await _modelRepository.GetAsync(status, visibility);
            var response = new List<object>();

            foreach (var model in models)
            {
                object? defaultVersionDto = null;
                IEnumerable<object>? presetsDto = null;

                if (includeSet.Contains("defaultversion") && !string.IsNullOrWhiteSpace(model.DefaultVersionId))
                {
                    var version = await _versionRepository.GetByIdAsync(model.DefaultVersionId);
                    if (version != null)
                    {
                        defaultVersionDto = new
                        {
                            version.Id,
                            version.VersionTag,
                            version.EndpointUrl,
                            version.Status
                        };
                    }
                }

                if (includeSet.Contains("presets"))
                {
                    var presets = await _presetRepository.GetByModelIdAsync(model.Id!, ImageModelPresetStatus.Active);
                    presetsDto = presets
                        .Where(p => p.Visibility == ImageModelVisibility.Public || p.Visibility == ImageModelVisibility.Premium)
                        .OrderBy(p => p.Ordering)
                        .Select(p => new
                        {
                            p.Id,
                            p.Slug,
                            p.Name,
                            p.Description,
                            p.Thumbnail,
                            p.Visibility,
                            p.Status,
                            p.Ordering
                        })
                        .ToList();
                }

                response.Add(new
                {
                    model.Id,
                    model.Slug,
                    model.DisplayName,
                    model.Visibility,
                    model.Status,
                    model.Tags,
                    DefaultVersion = defaultVersionDto,
                    Presets = presetsDto
                });
            }

            return Ok(response);
        }
    }
}
