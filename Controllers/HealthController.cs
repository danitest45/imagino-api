using Microsoft.AspNetCore.Mvc;
using Imagino.Api.Services.Generation;
using Imagino.Api.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Imagino.Api.Controllers;

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "ok" });
    [HttpGet("live")]
    public IActionResult Live() => Ok(new { status = "ok" });
    [HttpGet("ready")]
    public async Task<IActionResult> Ready([FromServices] IMongoClient mongo, [FromServices] IOptions<ImageGeneratorSettings> database,
        [FromServices] IOptions<GenerationSettings> generation, [FromServices] GenerationWorkerState worker, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));
        try {
            await mongo.GetDatabase(database.Value.MongoDatabase).RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: timeout.Token);
            var ready = !generation.Value.Enabled || worker.Healthy;
            return StatusCode(ready ? 200 : 503, new { status = ready ? "ready" : "unready", mongo = true, worker = ready,
                paidGenerationEnabled = generation.Value.PaidGenerationEnabled });
        } catch (Exception) { return StatusCode(503, new { status = "unready", mongo = false }); }
    }
}
