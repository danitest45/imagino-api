using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Imagino.Api.Errors;
using Imagino.Api.Services.Generation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Bson.Serialization;
using MongoDB.Bson;
using Moq;
using Xunit;

namespace Imagino.Api.Tests;

public class GenerationV2Tests
{
    private static GenerationModel Model(string id = "flux-fast-20261002") => GenerationCatalog.Seed(true).Single(m => m.Id == id);
    private static GenerationRequest Request(GenerationModel model) => new() { ModelId = model.Id, Prompt = "a test image" };
    private static GenerationJob Job(GenerationModel model) => new() { Model = model, UserId = "owner", Prompt = "private prompt",
        Settings = model.Fields.ToDictionary(f => f.Key, f => f.DefaultValue), Lease = "lease", DeadlineAt = DateTime.UtcNow.AddMinutes(10),
        Quote = GenerationPolicy.Quote(model, GenerationPolicy.Validate(model, Request(model)), DateTime.UtcNow) };
    private static IOptions<GenerationSettings> Settings(bool paid = false) => Options.Create(new GenerationSettings { Enabled = true, StagingFixtureEnabled = true, PaidGenerationEnabled = paid, BflApiKey = "test-key", GeminiApiKey = "test-key" });
    private static GenerationService Service(Mock<IGenerationRepository> repo, IGenerationProvider provider, bool paid = false) => new(repo.Object, new[] { provider }, Settings(paid));
    private static GenerationProcessor Processor(Mock<IGenerationRepository> repo, IGenerationProvider provider, Mock<IGenerationOutputStore> storage) => new(repo.Object,
        new[] { provider }, Service(repo, provider, true), new GenerationProviderHttp(Mock.Of<IHttpClientFactory>()), storage.Object, Settings(true), NullLogger<GenerationProcessor>.Instance);

    [Theory]
    [InlineData("flux-fast-20261002", "1MP", 5, 0.014)]
    [InlineData("flux-fast-20261002", "4MP", 6, 0.017)]
    [InlineData("flux-studio-20261002", "1MP", 10, 0.03)]
    [InlineData("flux-studio-20261002", "4MP", 25, 0.075)]
    public void Pricing_UsesResolutionAndRoundsUp(string id, string resolution, int credits, double usd)
    {
        var model = Model(id);
        var request = Request(model);
        request.Settings["resolution"] = JsonSerializer.SerializeToElement(resolution);
        var quote = GenerationPolicy.Quote(model, GenerationPolicy.Validate(model, request), DateTime.UtcNow);
        Assert.Equal(credits, quote.Credits);
        Assert.Equal((decimal)usd, quote.ProviderCostEstimateUsd);
    }
    [Fact]
    public void Pricing_VideoScalesByDurationAndResolution()
    {
        var model = Model("veo-fast-20261002");
        var request = Request(model);
        request.Settings["resolution"] = JsonSerializer.SerializeToElement("1080p");
        request.Settings["duration"] = JsonSerializer.SerializeToElement(8);
        var quote = GenerationPolicy.Quote(model, GenerationPolicy.Validate(model, request), DateTime.UtcNow);
        Assert.Equal(0.64m, quote.ProviderCostEstimateUsd);
        Assert.Equal(204, quote.Credits);
    }
    [Fact]
    public void Pricing_ReferencesAndPricingRevisionAreBoundToQuote()
    {
        var model = Model("flux-studio-20261002");
        var validated = GenerationPolicy.Validate(model, Request(model));
        validated.Inputs.Add(new("reference", "data"));
        var quote = GenerationPolicy.Quote(model, validated, DateTime.UtcNow);
        Assert.Equal(0.045m, quote.ProviderCostEstimateUsd);
        Assert.Equal(15, quote.Credits);
        var original = GenerationPolicy.Fingerprint(model, validated);
        model.Pricing.RatesUsd["1MP"] = 0.06m;
        Assert.NotEqual(original, GenerationPolicy.Fingerprint(model, validated));
    }
    [Theory]
    [InlineData("negativePrompt", "bad")]
    [InlineData("resolution", "8K")]
    [InlineData("aspectRatio", "100:1")]
    public void UnsupportedSettingsAreRejected(string key, string value)
    {
        var model = Model(); var request = Request(model);
        request.Settings[key] = JsonSerializer.SerializeToElement(value);
        Assert.Throws<ValidationAppException>(() => GenerationPolicy.Validate(model, request));
    }
    [Fact]
    public void SettingsHaveStrictTypesAndPromptLimits()
    {
        var model = Model("veo-fast-20261002"); var request = Request(model);
        request.Settings["duration"] = JsonSerializer.SerializeToElement("4");
        Assert.Throws<ValidationAppException>(() => GenerationPolicy.Validate(model, request));
        request.Settings.Clear(); request.Prompt = new string('x', 2001);
        Assert.Throws<ValidationAppException>(() => GenerationPolicy.Validate(model, request));
        request.Prompt = " "; Assert.Throws<ValidationAppException>(() => GenerationPolicy.Validate(model, request));
    }
    [Fact]
    public void UnsupportedImageCapabilityAndRemoteUrlsAreRejected()
    {
        var model = Model(); var request = Request(model);
        request.Inputs.Add(new("reference", "https://example.com/private.png"));
        Assert.Throws<ValidationAppException>(() => GenerationPolicy.Validate(model, request));
        Assert.Throws<ValidationAppException>(() => GenerationPolicy.ValidatePngInput(request.Inputs[0].Data));
        Assert.Throws<ValidationAppException>(() => GenerationPolicy.ValidatePngInput("data:image/png;base64,invalid"));
    }
    [Fact]
    public void ModelAndProviderDisableFailClosed()
    {
        var model = Model(); model.Enabled = false;
        Assert.Throws<ValidationAppException>(() => GenerationPolicy.Validate(model, Request(model)));
        model.Enabled = true; model.ProviderEnabled = false;
        Assert.Throws<ValidationAppException>(() => GenerationPolicy.Validate(model, Request(model)));
    }
    [Fact]
    public void UpcomingRetirementBlocksActivationEvenForOldActiveCatalogRows()
    {
        var repo = new Mock<IGenerationRepository>();
        var provider = new Mock<IGenerationProvider>();
        provider.SetupGet(p => p.Name).Returns("google-veo"); provider.SetupGet(p => p.IsConfigured).Returns(true);
        var model = Model("veo-fast-20261002");
        model.Lifecycle = "ACTIVE"; // A previously seeded document cannot bypass the safety schedule.
        var service = Service(repo, provider.Object, true);
        Assert.Equal("migration_required", service.Availability(model, new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc)));
        var retired = new DateTime(2026, 10, 22, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal("retired", service.Availability(model, retired));
        var input = GenerationPolicy.Validate(model, Request(model));
        Assert.Throws<ValidationAppException>(() => GenerationPolicy.Quote(model, input, retired));
        Assert.Null(GenerationLifecycle.RetirementAt(Model("gemini-edit-20261002")));
    }
    [Fact]
    public async Task MigratingVideoCannotReserveCreditsOrDispatchProvider()
    {
        var repo = new Mock<IGenerationRepository>(); var model = Model("veo-cinema-20261002");
        repo.Setup(r => r.CatalogAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<GenerationModel> { model });
        var provider = new Mock<IGenerationProvider>(); provider.SetupGet(p => p.Name).Returns("google-veo"); provider.SetupGet(p => p.IsConfigured).Returns(true);
        await Assert.ThrowsAsync<ForbiddenFeatureException>(() => Service(repo, provider.Object, true).CreateAsync("owner", "a-valid-key-123456", Request(model), default));
        repo.Verify(r => r.ReserveAsync(It.IsAny<GenerationJob>(), It.IsAny<CancellationToken>()), Times.Never);
        var job = Job(model); job.Status = GenerationStatus.Starting;
        await Processor(repo, provider.Object, new()).ProcessAsync(job, default);
        provider.Verify(p => p.StartAsync(It.IsAny<GenerationJob>(), It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(r => r.SettleAsync(job, GenerationStatus.Failed, null, "generation_disabled", It.IsAny<CancellationToken>()), Times.Once);
    }
    [Fact]
    public void VideoConstraintsRejectUnsupportedCombinations()
    {
        var model = Model("veo-fast-20261002"); var request = Request(model);
        request.Settings["resolution"] = JsonSerializer.SerializeToElement("1080p");
        Assert.Throws<ValidationAppException>(() => GenerationPolicy.Validate(model, request));
    }
    [Fact]
    public void QuotesExpireAndCannotBeReusedWithChangedSettings()
    {
        var model = Model(); var input = GenerationPolicy.Validate(model, Request(model));
        var quote = GenerationPolicy.Quote(model, input, DateTime.UtcNow);
        GenerationPolicy.ValidateQuote(quote.QuoteId, GenerationPolicy.Fingerprint(model, input), DateTime.UtcNow);
        Assert.Throws<ConflictAppException>(() => GenerationPolicy.ValidateQuote(quote.QuoteId, "other", DateTime.UtcNow));
        Assert.Throws<ConflictAppException>(() => GenerationPolicy.ValidateQuote(quote.QuoteId, GenerationPolicy.Fingerprint(model, input), DateTime.UtcNow.AddMinutes(11)));
    }
    [Fact]
    public async Task PaidGateRejectsBeforeCreditReservation()
    {
        var repo = new Mock<IGenerationRepository>(); var model = Model();
        repo.Setup(r => r.CatalogAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<GenerationModel> { model });
        var provider = new Mock<IGenerationProvider>(); provider.SetupGet(p => p.Name).Returns("bfl"); provider.SetupGet(p => p.IsConfigured).Returns(true);
        await Assert.ThrowsAsync<ForbiddenFeatureException>(() => Service(repo, provider.Object).CreateAsync("owner", "a-valid-key-123456", Request(model), default));
        repo.Verify(r => r.ReserveAsync(It.IsAny<GenerationJob>(), It.IsAny<CancellationToken>()), Times.Never);
        provider.Verify(p => p.StartAsync(It.IsAny<GenerationJob>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    [Fact]
    public async Task IdempotentRetryReturnsExistingJobWithoutAnotherReservation()
    {
        var repo = new Mock<IGenerationRepository>(); var model = Model("pipeline-demo-20261002"); var request = Request(model); var job = Job(model);
        job.RequestHash = GenerationPolicy.Fingerprint(model, GenerationPolicy.Validate(model, request));
        repo.Setup(r => r.CatalogAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<GenerationModel> { model });
        repo.Setup(r => r.FindByKeyAsync("owner", "a-valid-key-123456", It.IsAny<CancellationToken>())).ReturnsAsync(job);
        var result = await Service(repo, new StagingGenerationProvider(Settings())).CreateAsync("owner", "a-valid-key-123456", request, default);
        Assert.Same(job, result);
        repo.Verify(r => r.ReserveAsync(It.IsAny<GenerationJob>(), It.IsAny<CancellationToken>()), Times.Never);
        request.Prompt = "changed";
        await Assert.ThrowsAsync<ConflictAppException>(() => Service(repo, new StagingGenerationProvider(Settings())).CreateAsync("owner", "a-valid-key-123456", request, default));
    }
    [Fact]
    public async Task InsufficientCreditsPropagatesWithoutProviderDispatch()
    {
        var repo = new Mock<IGenerationRepository>(); var model = Model("pipeline-demo-20261002"); var request = Request(model);
        repo.Setup(r => r.CatalogAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<GenerationModel> { model });
        repo.Setup(r => r.ReserveAsync(It.IsAny<GenerationJob>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InsufficientCreditsException(0, 1));
        var service = Service(repo, new StagingGenerationProvider(Settings())); request.QuoteId = (await service.QuoteAsync(request, default)).QuoteId;
        await Assert.ThrowsAsync<InsufficientCreditsException>(() => service.CreateAsync("owner", "a-valid-key-123456", request, default));
    }
    [Fact]
    public async Task AmbiguousPostFailureIsRefundedAndNeverRetried()
    {
        var repo = new Mock<IGenerationRepository>(); var model = Model(); var job = Job(model); job.Status = GenerationStatus.Starting;
        repo.Setup(r => r.CatalogAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<GenerationModel> { model });
        var provider = new Mock<IGenerationProvider>(); provider.SetupGet(p => p.Name).Returns("bfl"); provider.SetupGet(p => p.IsConfigured).Returns(true);
        provider.Setup(p => p.StartAsync(job, It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("private URL must not be logged"));
        await Processor(repo, provider.Object, new()).ProcessAsync(job, default);
        provider.Verify(p => p.StartAsync(job, It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.SettleAsync(job, GenerationStatus.Failed, null, "submission_unknown", It.IsAny<CancellationToken>()), Times.Once);
    }
    [Fact]
    public async Task TimeoutRefundsWithoutCallingProvider()
    {
        var repo = new Mock<IGenerationRepository>(); var job = Job(Model()); job.Status = GenerationStatus.Processing; job.ProviderJobId = "bound"; job.DeadlineAt = DateTime.UtcNow.AddSeconds(-1);
        var provider = new Mock<IGenerationProvider>();
        await Processor(repo, provider.Object, new()).ProcessAsync(job, default);
        repo.Verify(r => r.SettleAsync(job, GenerationStatus.Failed, null, "generation_timeout", It.IsAny<CancellationToken>()), Times.Once);
        provider.Verify(p => p.PollAsync(It.IsAny<GenerationJob>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    [Fact]
    public async Task SafePollRetryDoesNotResubmitOrRefundPrematurely()
    {
        var repo = new Mock<IGenerationRepository>(); var job = Job(Model()); job.Status = GenerationStatus.Processing; job.ProviderJobId = "bound";
        var provider = new Mock<IGenerationProvider>(); provider.SetupGet(p => p.Name).Returns("bfl"); provider.SetupGet(p => p.IsConfigured).Returns(true);
        provider.Setup(p => p.PollAsync(job, It.IsAny<CancellationToken>())).ThrowsAsync(new ProviderCallException(503));
        await Processor(repo, provider.Object, new()).ProcessAsync(job, default);
        repo.Verify(r => r.DeferAsync(job, true, It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.SettleAsync(It.IsAny<GenerationJob>(), It.IsAny<GenerationStatus>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        provider.Verify(p => p.StartAsync(It.IsAny<GenerationJob>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    [Fact]
    public async Task StorageMustCompleteBeforeCreditsAreCharged()
    {
        var repo = new Mock<IGenerationRepository>(); var storage = new Mock<IGenerationOutputStore>(); var job = Job(Model()); job.Status = GenerationStatus.Processing;
        var provider = new Mock<IGenerationProvider>(); provider.SetupGet(p => p.Name).Returns("bfl"); provider.SetupGet(p => p.IsConfigured).Returns(true);
        provider.Setup(p => p.PollAsync(job, It.IsAny<CancellationToken>())).ReturnsAsync(new ProviderResult("id", null, Completed: true, Bytes: new byte[] { 1 }));
        storage.Setup(s => s.StoreAsync(job, It.IsAny<byte[]>(), It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException());
        await Processor(repo, provider.Object, storage).ProcessAsync(job, default);
        repo.Verify(r => r.DeferAsync(job, true, It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.SettleAsync(job, GenerationStatus.Completed, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    [Theory]
    [InlineData("https://api.us1.bfl.ai/v1/get_result?id=other", "bound")]
    [InlineData("https://evil.test/v1/get_result?id=bound", "bound")]
    [InlineData("https://api.us1.bfl.ai/anything?id=bound", "bound")]
    public void BflPollingRequiresProviderHostAndBoundJob(string url, string id) => Assert.ThrowsAny<Exception>(() => BflGenerationProvider.ValidatePolling(url, id));
    [Fact]
    public void BflPollingAcceptsReturnedRegionalEndpoint() => BflGenerationProvider.ValidatePolling("https://api.us1.bfl.ai/v1/get_result?id=bound", "bound");
    [Fact]
    public void SnapshotRoundTripsThroughMongoSerialization()
    {
        var job = Job(Model());
        var copy = BsonSerializer.Deserialize<GenerationJob>(job.ToBson());
        Assert.Equal(job.Quote.Credits, copy.Quote.Credits);
        Assert.Equal(job.Model.Fields[0].Options, copy.Model.Fields[0].Options);
    }
    [Fact]
    public void ProductionAndMismatchedStagingConfigurationsAreRejected()
    {
        var data = new Dictionary<string, string> { ["GenerationV2:Enabled"] = "true", ["ImageGeneratorSettings:MongoDatabase"] = "imagino_prod",
            ["R2Settings:BucketName"] = "imagino-images-staging", ["R2Settings:BucketNameVideos"] = "imagino-videos-staging" };
        Assert.Throws<InvalidOperationException>(() => GenerationRegistration.ValidateStaging(new ConfigurationBuilder().AddInMemoryCollection(data).Build()));
        data["ImageGeneratorSettings:MongoDatabase"] = "imagino_staging";
        GenerationRegistration.ValidateStaging(new ConfigurationBuilder().AddInMemoryCollection(data).Build());
        data["R2Settings:BucketName"] = "production-images";
        Assert.Throws<InvalidOperationException>(() => GenerationRegistration.ValidateStaging(new ConfigurationBuilder().AddInMemoryCollection(data).Build()));
    }
    [Theory]
    [InlineData("Created", "Queued")]
    [InlineData("Running", "Processing")]
    [InlineData("Completed", "Completed")]
    public void LegacyStateNormalizationIsNonDestructive(string old, string normalized) => Assert.Equal(normalized, GenerationPolicy.NormalizeLegacyStatus(old));
}
