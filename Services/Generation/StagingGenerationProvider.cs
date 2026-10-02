using Microsoft.Extensions.Options;

namespace Imagino.Api.Services.Generation;

public sealed class StagingGenerationProvider(IOptions<GenerationSettings> options) : IGenerationProvider
{
    public string Name => "fixture";
    public bool IsConfigured => options.Value.StagingFixtureEnabled;
    public Task<ProviderResult> StartAsync(GenerationJob job, CancellationToken ct) =>
        Task.FromResult(new ProviderResult("fixture-" + job.Id, null));
    public async Task<ProviderResult> PollAsync(GenerationJob job, CancellationToken ct)
    {
        if (job.ProviderJobId != "fixture-" + job.Id) throw new InvalidDataException("Fixture binding mismatch.");
        if (job.Settings.GetValueOrDefault("outcome") == "failure")
            return new ProviderResult(job.ProviderJobId, null, ErrorCode: "synthetic_provider_failure");
        return new ProviderResult(job.ProviderJobId, null, Completed: true,
            Bytes: await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "generation-demo.png"), ct));
    }
}
