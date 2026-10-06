using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Imagino.Api.Errors;
using Imagino.Api.Security;
using Imagino.Api.Services.Generation;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Moq;
using Xunit;

namespace Imagino.Api.Tests;

public sealed class OpenAiSingleSmokeTests
{
    private static GenerationJob Job()
    {
        var model = GenerationCatalog.Seed(true).Single(m => m.Id == "openai-fast-20261006");
        var input = GenerationPolicy.Validate(model, new() { ModelId = model.Id, Prompt = BflHomologationPolicy.FastPrompt });
        return new() { UserId = BflHomologationPolicy.OwnerId, Model = model, Prompt = input.Prompt, Settings = input.Settings,
            Inputs = input.Inputs, IdempotencyKey = OpenAiSingleSmokePolicy.Key, OpenAiRunId = OpenAiSingleSmokePolicy.RunId,
            OpenAiHomologationCall = 1, RequestHash = GenerationPolicy.Fingerprint(model, input), Status = GenerationStatus.Starting,
            Lease = "one-lease", DeadlineAt = DateTime.UtcNow.AddMinutes(10),
            Quote = GenerationPolicy.Quote(model, input, DateTime.UtcNow, singleSmoke: true) };
    }
    private static IOptions<GenerationSettings> OptionsFor(bool authorized = true) => Options.Create(new GenerationSettings {
        Enabled = true, OpenAiHomologationEnabled = true, OpenAiSingleSmokeEnabled = authorized,
        PaidGenerationEnabled = authorized, OpenAiApiKey = "unit-only", StagingFixtureEnabled = true });
    [Fact]
    public void ProjectionIsAnEstimateAndOldGuaranteedThreeCallPlanRemainsClosed()
    {
        Assert.Equal(0.01573m, OpenAiSingleSmokePolicy.ProjectionUsd);
        Assert.True(OpenAiSingleSmokePolicy.ProjectionUsd < 0.10m / 2);
        Assert.Null(OpenAiHomologationPolicy.VerifiedMaximumUsd(1));
        Assert.False(OpenAiHomologationPolicy.CostBoundsVerified);
        Assert.Equal(6, Job().Quote.Credits);
        Assert.Equal(OpenAiSingleSmokePolicy.ProjectionUsd, Job().Quote.ProviderCostEstimateUsd);
    }
    [Theory]
    [InlineData("Reserved")] [InlineData("SubmissionAttempted")] [InlineData("ResponseReceived")]
    [InlineData("Completed")] [InlineData("Failed")]
    public void ConsumedSingleSlotCannotReopenAfterSerialization(string state)
    {
        var ledger = OpenAiSingleSmokePolicy.NewLedger();
        Assert.True(OpenAiSingleSmokePolicy.CanReserve(ledger, DateTime.UtcNow));
        ledger.Calls["1"].State = state; ledger.Calls["1"].JobId = Job().Id;
        var restored = BsonSerializer.Deserialize<OpenAiHomologationLedger>(ledger.ToBson());
        Assert.Single(restored.Calls); Assert.Equal(0, restored.Calls["1"].MaximumUsd);
        Assert.Equal(0.01573m, restored.Calls["1"].ProjectedUsd);
        Assert.False(OpenAiSingleSmokePolicy.CanReserve(restored, DateTime.UtcNow));
    }
    [Fact]
    public void ExtraSlotHaltExpiryOwnerBudgetAndExistingAttemptRejectReservation()
    {
        foreach (var change in new Action<OpenAiHomologationLedger>[] {
            l => l.Calls["2"] = new(), l => l.Halted = true, l => l.ExpiresAtUtc = DateTime.UtcNow.AddSeconds(-1),
            l => l.OwnerId = "foreign", l => l.BudgetUsd = 0.50m, l => l.Calls["1"].AttemptedAtUtc = DateTime.UtcNow,
            l => l.Calls["1"].MaximumUsd = 0.10m, l => l.CommittedUsd = 0.01m })
        { var ledger = OpenAiSingleSmokePolicy.NewLedger(); change(ledger); Assert.False(OpenAiSingleSmokePolicy.CanReserve(ledger, DateTime.UtcNow)); }
    }
    [Fact]
    public void OnlyExactFlareRequestAndFixedKeyAreAccepted()
    {
        OpenAiSingleSmokePolicy.ValidateJob(Job());
        foreach (var change in new Action<GenerationJob>[] {
            j => j.UserId = "foreign", j => j.Prompt += " extra", j => j.Model.ProviderModel = OpenAiHomologationPolicy.Sunburst,
            j => j.IdempotencyKey = "second-single-smoke-slot", j => j.OpenAiRunId = OpenAiHomologationPolicy.RunId,
            j => j.Inputs.Add(new("reference", "unused")), j => j.Settings["quality"] = "high", j => j.RequestHash = "tampered" })
        { var job = Job(); change(job); Assert.Throws<ForbiddenFeatureException>(() => OpenAiSingleSmokePolicy.ValidateJob(job)); }
    }
    [Fact]
    public void WindowBlocksEveryOtherPaidOfferAndClosureBlocksFlare()
    {
        var opts = OptionsFor(); var provider = new Mock<IGenerationProvider>(); provider.SetupGet(p => p.Name).Returns("openai"); provider.SetupGet(p => p.IsConfigured).Returns(true);
        var service = new GenerationService(Mock.Of<IGenerationRepository>(), new[] { provider.Object }, opts);
        foreach (var model in GenerationCatalog.Seed(true).Where(m => m.Provider != "fixture"))
            if (OpenAiSingleSmokePolicy.AllowsModel(model)) Assert.Equal("ready", service.Availability(model));
            else Assert.DoesNotContain(service.Availability(model), new[] { "ready", "synthetic_demo" });
        opts.Value.PaidGenerationEnabled = false; opts.Value.OpenAiSingleSmokeEnabled = false;
        Assert.Equal("approval_required", service.Availability(Job().Model));
    }
    [Fact]
    public async Task ClosedAuthorizationRejectsAdapterBeforeAnyHttpPost()
    {
        var factory = new Mock<IHttpClientFactory>();
        await Assert.ThrowsAsync<ForbiddenFeatureException>(() => new OpenAiImageGenerationProvider(new(factory.Object), OptionsFor(false)).StartAsync(Job(), default));
        factory.Verify(f => f.CreateClient(It.IsAny<string>()), Times.Never);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task SynchronousSingleSmokeStoresOnlyWithinObservedCeilingAndNeverPolls(bool exceeds)
    {
        var job = Job(); var opts = OptionsFor(); var repo = new Mock<IGenerationRepository>();
        repo.Setup(r => r.CatalogAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<GenerationModel> { job.Model });
        repo.Setup(r => r.BeginOpenAiSubmissionAsync(job, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var outputTokens = exceeds ? 4000 : 439;
        var usage = new GenerationUsage(48, 0, outputTokens, 48, outputTokens, 48 + outputTokens, 0, OpenAiImagePricing.Revision);
        var provider = new Mock<IGenerationProvider>(); provider.SetupGet(p => p.Name).Returns("openai"); provider.SetupGet(p => p.IsConfigured).Returns(true);
        var bytes = System.IO.File.ReadAllBytes(System.IO.Path.Combine(AppContext.BaseDirectory, "Fixtures", "bfl-reference-20261005.png"));
        provider.Setup(p => p.StartAsync(job, It.IsAny<CancellationToken>())).ReturnsAsync(new ProviderResult(null, null, true, bytes,
            CostUsd: OpenAiImagePricing.Calculate(usage), Usage: usage));
        var store = new Mock<IGenerationOutputStore>(); store.Setup(s => s.StoreAsync(job, It.IsAny<byte[]>(), It.IsAny<CancellationToken>())).ReturnsAsync("stored.png");
        var service = new GenerationService(repo.Object, new[] { provider.Object }, opts);
        await new GenerationProcessor(repo.Object, new[] { provider.Object }, service, new(Mock.Of<IHttpClientFactory>()), store.Object, opts,
            NullLogger<GenerationProcessor>.Instance).ProcessAsync(job, default);
        provider.Verify(p => p.StartAsync(job, It.IsAny<CancellationToken>()), Times.Once);
        provider.Verify(p => p.PollAsync(It.IsAny<GenerationJob>(), It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(r => r.RecordSynchronousResultAsync(job, It.IsAny<ProviderResult>(), It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.SettleAsync(job, exceeds ? GenerationStatus.Failed : GenerationStatus.Completed,
            exceeds ? null : "stored.png", exceeds ? "observed_ceiling_exceeded" : null, It.IsAny<CancellationToken>()), Times.Once);
        store.Verify(s => s.StoreAsync(job, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), exceeds ? Times.Never() : Times.Once());
    }
    [Fact]
    public void SingleSmokeConfigRequiresExplicitProviderScope()
    {
        var values = new Dictionary<string, string> { ["ASPNETCORE_ENVIRONMENT"] = "AIStaging", ["RENDER_SERVICE_ID"] = BflHomologationPolicy.ServiceId,
            ["RENDER_GIT_BRANCH"] = "codex/imagino-ai-revival-v2", ["RENDER_EXTERNAL_HOSTNAME"] = "imagino-api-ai-staging.onrender.com",
            ["GenerationV2:OpenAiHomologationEnabled"] = "true", ["GenerationV2:OpenAiSingleSmokeEnabled"] = "true", ["GenerationV2:PaidGenerationEnabled"] = "true" };
        AIStagingConfiguration.Validate(AIStagingTests.Valid(values));
        values["GenerationV2:OpenAiHomologationEnabled"] = "false";
        Assert.Throws<InvalidOperationException>(() => AIStagingConfiguration.Validate(AIStagingTests.Valid(values)));
    }
}
