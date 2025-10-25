using System.Collections.Generic;
using Imagino.Api.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Imagino.Api.Controllers
{
    [ApiController]
    [Route("api/replicate/models")]
    public class ReplicateModelsController : ControllerBase
    {
        private readonly ReplicateSettings _settings;

        public ReplicateModelsController(IOptions<ReplicateSettings> settings)
        {
            _settings = settings.Value;
        }

        [HttpGet]
        public ActionResult<IEnumerable<ReplicateModel>> GetModels()
        {
            return Ok(_settings.Models);
        }
    }
}
