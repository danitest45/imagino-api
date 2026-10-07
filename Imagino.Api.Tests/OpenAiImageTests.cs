using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Imagino.Api.Errors;
using Imagino.Api.Controllers;
using Imagino.Api.Security;
using Imagino.Api.Services.Generation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Moq;
using Xunit;

namespace Imagino.Api.Tests;

public sealed class OpenAiImageTests
{
    private static byte[] Png() => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "bfl-reference-20261005.png"));
    private static IOptions<GenerationSettings> Settings() => Options.Create(new GenerationSettings { Enabled = true,
        OpenAiApiKey = "unit-secret-never-real", OpenAiHomologationEnabled = true, PaidGenerationEnabled = false, StagingFixtureEnabled = true });
    private static GenerationJob Job(int call = 1)
    {
        var model = GenerationCatalog.Seed(true).Single(m => m.Id == (call == 1 ? "openai-fast-20261006" : "openai-studio-20261006"));
        var request = new GenerationRequest { ModelId = model.Id, Prompt = call switch { 1 => BflHomologationPolicy.FastPrompt,
            2 => BflHomologationPolicy.StudioPrompt, _ => BflHomologationPolicy.ReferencePrompt } };
        if (call == 3) request.Inputs.Add(new("reference", "data:image/png;base64," + Convert.ToBase64String(Png())));
        var input = GenerationPolicy.Validate(model, request);
        return new() { UserId = BflHomologationPolicy.OwnerId, Model = model, Prompt = input.Prompt, Settings = input.Settings, Inputs = input.Inputs,
            RequestHash = GenerationPolicy.Fingerprint(model, input), IdempotencyKey = $"openai-homologation-20261006-call-{call}",
            Status = GenerationStatus.Starting, Lease = "test-lease", DeadlineAt = DateTime.UtcNow.AddMinutes(10),
            Quote = new("mock-quote", model.Id, model.Version, 5, 0.01317m, 0.016487m, model.Pricing.Revision, DateTime.UtcNow.AddMinutes(10), input.Settings) };
    }
    private static string Response(byte[]? bytes = null) => JsonSerializer.Serialize(new { data = new[] { new { b64_json = Convert.ToBase64String(bytes ?? Png()) } },
        usage = new { input_tokens = 150, input_tokens_details = new { text_tokens = 50, image_tokens = 100, cached_tokens = 50 },
            output_tokens = 439, total_tokens = 589, output_tokens_details = new { image_tokens = 439, text_tokens = 0 } },
        private_field = "provider-raw-secret-must-not-escape" });
    private sealed class Wire(string response, HttpStatusCode status = HttpStatusCode.OK, bool timeout = false) : HttpMessageHandler, IHttpClientFactory
    {
        public List<(string Method, string Url, string Body, string Authorization)> Requests { get; } = new();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add((request.Method.Method, request.RequestUri!.ToString(), await request.Content!.ReadAsStringAsync(ct), request.Headers.GetValues("Authorization").Single()));
            if (timeout) throw new TaskCanceledException("unit-secret-never-real private URL");
            return new(status) { Content = new StringContent(response) };
        }
        public HttpClient CreateClient(string name) => new(this, false);
    }
    [Theory]
    [InlineData(1, "generations", OpenAiHomologationPolicy.Flare)]
    [InlineData(2, "generations", OpenAiHomologationPolicy.Sunburst)]
    [InlineData(3, "edits", OpenAiHomologationPolicy.Sunburst)]
    public async Task DirectContractMapsOnlyTheApprovedSettingsAndReturnsAnInlineResult(int call, string endpoint, string model)
    {
        var wire = new Wire(Response()); var job = Job(call);
        var provider = new OpenAiImageGenerationProvider(new(wire), Settings());
        var result = await provider.StartAsync(job, default);
        Assert.True(result.Completed); Assert.Null(result.JobId); Assert.Null(result.PollingUrl); Assert.Null(result.OutputUrl);
        Assert.Equal(Png(), result.Bytes); Assert.NotNull(result.AcceptanceLatencyMs); Assert.Single(wire.Requests);
        var request = wire.Requests.Single(); Assert.Equal("POST", request.Method); Assert.Equal("https://api.openai.com/v1/images/" + endpoint, request.Url);
        Assert.Equal("Bearer unit-secret-never-real", request.Authorization);
        using var body = JsonDocument.Parse(request.Body); var root = body.RootElement;
        Assert.Equal(model, root.GetProperty("model").GetString()); Assert.Equal(job.Prompt, root.GetProperty("prompt").GetString());
        Assert.Equal("1024x1024", root.GetProperty("size").GetString()); Assert.Equal("medium", root.GetProperty("quality").GetString());
        Assert.Equal("png", root.GetProperty("output_format").GetString()); Assert.Equal("opaque", root.GetProperty("background").GetString());
        Assert.Equal(1, root.GetProperty("n").GetInt32()); Assert.False(root.GetProperty("stream").GetBoolean());
        foreach (var forbidden in new[] { "response_format", "input_fidelity", "mask", "partial_images", "tools" }) Assert.False(root.TryGetProperty(forbidden, out _));
        if (call == 3) { Assert.Single(root.GetProperty("images").EnumerateArray()); Assert.Equal(job.Inputs[0].Data, root.GetProperty("images")[0].GetProperty("image_url").GetString()); }
        else Assert.False(root.TryGetProperty("images", out _));
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.PollAsync(job, default));
    }
    [Fact]
    public void NumericUsageIncludesReferenceInputAndNeverAppliesDirectApiCacheDiscounts()
    {
        using var document = JsonDocument.Parse(Response()); var result = OpenAiImageGenerationProvider.ParseResponse(document.RootElement);
        Assert.Equal(new GenerationUsage(50, 100, 439, 150, 439, 589, 50, OpenAiImagePricing.Revision), result.Usage);
        Assert.Equal(0.01422m, result.CostUsd); Assert.Equal(6, OpenAiImagePricing.ExperimentalCredits(result.CostUsd!.Value, Job().Model.Pricing));
        var persisted = JsonSerializer.Serialize(result.Usage);
        Assert.DoesNotContain("unit-secret", persisted); Assert.DoesNotContain("provider-raw", persisted); Assert.DoesNotContain("b64_json", persisted);
    }
    [Theory]
    [InlineData("{\"data\":[]}")]
    [InlineData("{\"usage\":{\"input_tokens\":-1}}")]
    [InlineData("{\"usage\":{\"input_tokens\":\"150\"}}")]
    [InlineData("{\"usage\":{\"input_tokens\":150,\"output_tokens\":439,\"total_tokens\":589}}")]
    public void MissingMalformedOrNegativeUsageIsRejected(string response)
    {
        using var document = JsonDocument.Parse(response);
        Assert.Throws<InvalidDataException>(() => OpenAiImageGenerationProvider.ParseResponse(document.RootElement));
    }
    [Theory]
    [InlineData("\"total_tokens\":589", "\"total_tokens\":590")]
    [InlineData("\"image_tokens\":100", "\"image_tokens\":99")]
    [InlineData("\"text_tokens\":0", "\"text_tokens\":1")]
    [InlineData("\"cached_tokens\":50", "\"cached_tokens\":151")]
    public void InconsistentOrUnpricedUsageFailsClosed(string oldValue, string newValue)
    {
        using var document = JsonDocument.Parse(Response().Replace(oldValue, newValue));
        Assert.Throws<InvalidDataException>(() => OpenAiImageGenerationProvider.ParseResponse(document.RootElement));
    }
    [Fact]
    public void OutputWithoutDetailedBreakdownStillUsesTheImageOutputTokenTotal()
    {
        using var document = JsonDocument.Parse(Response().Replace(",\"output_tokens_details\":{\"image_tokens\":439,\"text_tokens\":0}", ""));
        Assert.Equal(0.01422m, OpenAiImageGenerationProvider.ParseResponse(document.RootElement).CostUsd);
    }
    [Theory]
    [InlineData(400)] [InlineData(401)] [InlineData(403)] [InlineData(429)] [InlineData(500)]
    public async Task ModerationAndHttpFailuresContainOnlyStatusAndDoNotRetry(int status)
    {
        var wire = new Wire("{\"error\":{\"message\":\"private prompt unit-secret-never-real moderation\"}}", (HttpStatusCode)status);
        var error = await Assert.ThrowsAsync<ProviderCallException>(() => new OpenAiImageGenerationProvider(new(wire), Settings()).StartAsync(Job(), default));
        Assert.Equal(status, error.Status); Assert.DoesNotContain("private", error.Message); Assert.DoesNotContain("unit-secret", error.ToString()); Assert.Single(wire.Requests);
    }
    [Fact]
    public async Task TimeoutHasExactlyOneHttpPostAttempt()
    {
        var wire = new Wire("", timeout: true);
        await Assert.ThrowsAsync<TaskCanceledException>(() => new OpenAiImageGenerationProvider(new(wire), Settings()).StartAsync(Job(), default));
        Assert.Single(wire.Requests);
    }
    [Theory]
    [InlineData("size", "1536x1024")] [InlineData("quality", "auto")] [InlineData("quality", "high")]
    [InlineData("outputFormat", "jpeg")] [InlineData("background", "transparent")]
    public async Task UnsupportedConfigurationsNeverReachTheHttpHandler(string field, string value)
    {
        var wire = new Wire(Response()); var job = Job(); job.Settings[field] = value;
        await Assert.ThrowsAsync<ForbiddenFeatureException>(() => new OpenAiImageGenerationProvider(new(wire), Settings()).StartAsync(job, default));
        Assert.Empty(wire.Requests);
    }
    [Fact]
    public async Task UnsupportedSnapshotOwnerBriefOrReferenceCannotReachHttp()
    {
        foreach (var change in new Action<GenerationJob>[] { j => j.Model.ProviderModel = "gpt-image-1", j => j.UserId = "foreign",
            j => j.Prompt += " extra", j => j.IdempotencyKey = "fourth-call", j => j.RequestHash = "tampered" })
        {
            var job = Job(); change(job); var wire = new Wire(Response());
            await Assert.ThrowsAsync<ForbiddenFeatureException>(() => new OpenAiImageGenerationProvider(new(wire), Settings()).StartAsync(job, default));
            Assert.Empty(wire.Requests);
        }
        var reference = Job(3); reference.Inputs.Add(reference.Inputs[0]);
        Assert.Throws<ForbiddenFeatureException>(() => OpenAiHomologationPolicy.ValidateJob(reference));
        reference = Job(3); var bytes = Png(); bytes[^1] ^= 1;
        reference.Inputs[0] = new("reference", "data:image/png;base64," + Convert.ToBase64String(bytes));
        Assert.Throws<ForbiddenFeatureException>(() => OpenAiHomologationPolicy.ValidateJob(reference));
    }
    [Fact]
    public void Base64IsBoundedAndInvalidPngIsRejectedBeforeStorage()
    {
        using var malformed = JsonDocument.Parse(Response().Replace(JsonSerializer.Serialize(Convert.ToBase64String(Png())), JsonSerializer.Serialize("not-base64!")));
        var invalid = OpenAiImageGenerationProvider.ParseResponse(malformed.RootElement);
        Assert.Equal("invalid_provider_output", invalid.ErrorCode); Assert.NotNull(invalid.Usage); Assert.Equal(0.01422m, invalid.CostUsd); Assert.Null(invalid.Bytes);
        using var oversized = JsonDocument.Parse(Response().Replace(JsonSerializer.Serialize(Convert.ToBase64String(Png())), JsonSerializer.Serialize(new string('A', GeneratedImageValidator.MaxBase64Chars + 1))));
        Assert.Equal("invalid_provider_output", OpenAiImageGenerationProvider.ParseResponse(oversized.RootElement).ErrorCode);
        using var badPng = JsonDocument.Parse(Response(new byte[40])); var parsed = OpenAiImageGenerationProvider.ParseResponse(badPng.RootElement);
        Assert.NotNull(parsed.Usage); Assert.Throws<InvalidDataException>(() => BflHomologationPolicy.ValidateOutput(parsed.Bytes!));
        var wrongSize = Png(); wrongSize[19] = 1; Assert.Throws<InvalidDataException>(() => BflHomologationPolicy.ValidateOutput(wrongSize));
    }
    [Fact]
    public void FiniteLedgerSurvivesSerializationAndNeverAuthorizesAFourthOrAnUnboundedRequest()
    {
        var ledger = new OpenAiHomologationLedger();
        Assert.False(OpenAiHomologationPolicy.CanReserve(ledger, 1, null, DateTime.UtcNow));
        // These maxima are simulated boundary-test data, never provider quotes/production authorization.
        Assert.True(OpenAiHomologationPolicy.CanReserve(ledger, 1, 0.5m, DateTime.UtcNow));
        Assert.False(OpenAiHomologationPolicy.CanReserve(ledger, 1, 0.50000001m, DateTime.UtcNow));
        Assert.False(OpenAiHomologationPolicy.CanReserve(ledger, 2, 0.1m, DateTime.UtcNow));
        ledger.Calls["1"].State = "Completed"; ledger.Calls["1"].Reconciled = true; ledger.CommittedUsd = 0.41m;
        Assert.False(OpenAiHomologationPolicy.CanReserve(ledger, 2, 0.1m, DateTime.UtcNow));
        Assert.True(OpenAiHomologationPolicy.CanReserve(ledger, 2, 0.09m, DateTime.UtcNow));
        ledger.Calls["2"].State = "SubmissionAttempted"; ledger.Calls["2"].JobId = "attempted-once";
        var copy = BsonSerializer.Deserialize<OpenAiHomologationLedger>(ledger.ToBson());
        Assert.Equal(3, copy.Calls.Count); Assert.Equal("attempted-once", copy.Calls["2"].JobId);
        Assert.False(OpenAiHomologationPolicy.CanReserve(copy, 2, 0.09m, DateTime.UtcNow));
        Assert.False(OpenAiHomologationPolicy.CanReserve(copy, 3, 0.09m, DateTime.UtcNow));
        Assert.False(OpenAiHomologationPolicy.CanReserve(copy, 4, 0.01m, DateTime.UtcNow));
        Assert.Equal(BsonType.Decimal128, copy.ToBsonDocument()["BudgetUsd"].BsonType);
        copy.Halted = true; Assert.False(OpenAiHomologationPolicy.CanReserve(copy, 1, 0.01m, DateTime.UtcNow));
    }
    [Fact]
    public async Task MissingVerifiedBoundsBlocksQuoteReservationAndWorkerEvenWithAKeyAndPaidFlag()
    {
        var job = Job(); var opts = Settings(); opts.Value.PaidGenerationEnabled = true;
        var repository = new Mock<IGenerationRepository>(); repository.Setup(r => r.CatalogAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<GenerationModel> { job.Model });
        var provider = new Mock<IGenerationProvider>(); provider.SetupGet(p => p.Name).Returns("openai"); provider.SetupGet(p => p.IsConfigured).Returns(true);
        var service = new GenerationService(repository.Object, new[] { provider.Object }, opts);
        Assert.Equal("approval_required", service.Availability(job.Model)); Assert.False(OpenAiHomologationPolicy.CostBoundsVerified);
        await Assert.ThrowsAsync<ForbiddenFeatureException>(() => service.QuoteAsync(new() { ModelId = job.Model.Id, Prompt = job.Prompt }, default, job.UserId));
        await Assert.ThrowsAsync<ForbiddenFeatureException>(() => service.CreateAsync(job.UserId, job.IdempotencyKey, new() { ModelId = job.Model.Id, Prompt = job.Prompt }, default));
        await new GenerationProcessor(repository.Object, new[] { provider.Object }, service, new(Mock.Of<IHttpClientFactory>()), Mock.Of<IGenerationOutputStore>(), opts,
            NullLogger<GenerationProcessor>.Instance).ProcessAsync(job, default);
        repository.Verify(r => r.ReserveAsync(It.IsAny<GenerationJob>(), It.IsAny<CancellationToken>()), Times.Never);
        provider.Verify(p => p.StartAsync(It.IsAny<GenerationJob>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    [Theory]
    [InlineData("RENDER_SERVICE_ID", "other")]
    [InlineData("RENDER_GIT_BRANCH", "master")]
    [InlineData("ASPNETCORE_ENVIRONMENT", "Production")]
    [InlineData("GenerationV2:PaidGenerationEnabled", "true")]
    public void OpenAiConfigurationRejectsWrongScopeOrUnboundedSpending(string key, string value)
    {
        var values = Scope(); values[key] = value;
        Assert.Throws<InvalidOperationException>(() => AIStagingConfiguration.Validate(AIStagingTests.Valid(values)));
    }
    private static Dictionary<string, string> Scope() => new() { ["ASPNETCORE_ENVIRONMENT"] = "AIStaging",
        ["RENDER_SERVICE_ID"] = BflHomologationPolicy.ServiceId, ["RENDER_GIT_BRANCH"] = "codex/imagino-ai-revival-v2",
        ["RENDER_EXTERNAL_HOSTNAME"] = "imagino-api-ai-staging.onrender.com", ["GenerationV2:OpenAiHomologationEnabled"] = "true",
        ["GenerationV2:OpenAiApiKey"] = "unit-secret-never-real" };
    [Fact]
    public void IsolatedRegistrationLeavesOtherPaidProvidersUnregisteredAndQuotesUseTokenPricing()
    {
        var config = AIStagingTests.Valid(Scope()); AIStagingConfiguration.Validate(config);
        var services = new ServiceCollection(); services.AddGenerationV2(config, fixtureOnly: true);
        Assert.Equal(new[] { typeof(OpenAiImageGenerationProvider), typeof(StagingGenerationProvider) },
            services.Where(s => s.ServiceType == typeof(IGenerationProvider)).Select(s => s.ImplementationType));
        foreach (var model in GenerationCatalog.Seed(false).Where(m => m.Provider == "openai"))
        { Assert.Equal("approval_required", model.Availability); Assert.Equal("token", model.Pricing.Unit); Assert.Equal(OpenAiImagePricing.Revision, model.Pricing.Revision); }
    }
    [Fact]
    public async Task ClosedOpenAiOffersSerializeInTheSharedCatalogWithoutAttemptingPaidQuotes()
    {
        var repository = new Mock<IGenerationRepository>();
        repository.Setup(r => r.CatalogAsync(It.IsAny<CancellationToken>())).ReturnsAsync(GenerationCatalog.Seed(true));
        var opts = Settings(); var service = new GenerationService(repository.Object, Array.Empty<IGenerationProvider>(), opts);
        var controller = new GenerationController(repository.Object, service, Mock.Of<IGenerationOutputStore>(), opts);
        var response = Assert.IsType<OkObjectResult>(await controller.Catalog(default));
        var json = JsonSerializer.SerializeToElement(response.Value);
        var offers = json.GetProperty("models").EnumerateArray().Where(m => m.GetProperty("Id").GetString()!.StartsWith("openai-")).ToArray();
        Assert.Equal(2, offers.Length);
        foreach (var offer in offers) { Assert.Equal("approval_required", offer.GetProperty("availability").GetString()); Assert.Equal(5, offer.GetProperty("startingCredits").GetInt32()); }
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task SynchronousPipelineStoresBeforeSettlementAndRefundsAStorageFailureWithoutPolling(bool storageFails)
    {
        var job = Job(); job.Model = GenerationCatalog.Seed(true).Single(m => m.Provider == "fixture");
        var repository = new Mock<IGenerationRepository>(); repository.Setup(r => r.CatalogAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<GenerationModel> { job.Model });
        var order = new List<string>(); var provider = new Mock<IGenerationProvider>();
        provider.SetupGet(p => p.Name).Returns("fixture"); provider.SetupGet(p => p.IsConfigured).Returns(true);
        provider.Setup(p => p.StartAsync(job, It.IsAny<CancellationToken>())).ReturnsAsync(new ProviderResult(null, null, Completed: true, Bytes: Png()));
        repository.Setup(r => r.RecordSynchronousResultAsync(job, It.IsAny<ProviderResult>(), It.IsAny<CancellationToken>())).Callback(() => order.Add("response")).Returns(Task.CompletedTask);
        var storage = new Mock<IGenerationOutputStore>(); storage.Setup(s => s.StoreAsync(job, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("storage")).Returns(() => storageFails ? Task.FromException<string>(new HttpRequestException("private")) : Task.FromResult("stored.png"));
        repository.Setup(r => r.SettleAsync(job, It.IsAny<GenerationStatus>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("settle")).ReturnsAsync(true);
        var opts = Settings(); var service = new GenerationService(repository.Object, new[] { provider.Object }, opts);
        await new GenerationProcessor(repository.Object, new[] { provider.Object }, service, new(Mock.Of<IHttpClientFactory>()), storage.Object, opts, NullLogger<GenerationProcessor>.Instance).ProcessAsync(job, default);
        Assert.Equal(new[] { "response", "storage", "settle" }, order);
        provider.Verify(p => p.PollAsync(It.IsAny<GenerationJob>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(r => r.BindAsync(It.IsAny<GenerationJob>(), It.IsAny<ProviderResult>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(r => r.SettleAsync(job, storageFails ? GenerationStatus.Failed : GenerationStatus.Completed,
            storageFails ? null : "stored.png", storageFails ? "output_storage_failed" : null, It.IsAny<CancellationToken>()), Times.Once);
    }
    [Fact]
    public async Task RestartAfterSyncResponseFailsWithoutRepostingAndKeepsKnownUsage()
    {
        var job = Job(); job.SynchronousResponseAtUtc = DateTime.UtcNow.AddMinutes(-15); job.DeadlineAt = DateTime.UtcNow.AddMinutes(-5);
        var repository = new Mock<IGenerationRepository>(); var provider = new Mock<IGenerationProvider>(); var opts = Settings();
        var service = new GenerationService(repository.Object, new[] { provider.Object }, opts);
        await new GenerationProcessor(repository.Object, new[] { provider.Object }, service, new(Mock.Of<IHttpClientFactory>()), Mock.Of<IGenerationOutputStore>(), opts,
            NullLogger<GenerationProcessor>.Instance).ProcessAsync(job, default);
        provider.Verify(p => p.StartAsync(It.IsAny<GenerationJob>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(r => r.SettleAsync(job, GenerationStatus.Failed, null, "synchronous_result_lost", It.IsAny<CancellationToken>()), Times.Once);
    }
}
