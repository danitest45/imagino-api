using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Imagino.Api.Controllers;
using Imagino.Api.Errors;
using Imagino.Api.Security;
using Imagino.Api.Services.Generation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Moq;
using Xunit;

namespace Imagino.Api.Tests;

public class RunwayVideoTests
{
    const string TaskId = "d2e3d1f4-1b3c-4b5c-8d46-1c1d7ee86892";
    static readonly DateTime WithinAuthorization = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
    static byte[] Source() => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "runway-source-owned.png"));
    static GenerationModel Model() => GenerationCatalog.Seed(true).Single(m => m.Id == RunwaySmokePolicy.ModelId);
    static IOptions<GenerationSettings> Settings() => Options.Create(new GenerationSettings { Enabled = true, PaidGenerationEnabled = true,
        RunwayIntegrationEnabled = true, RunwayRealSmokeEnabled = true, RunwayApiKey = "unit-only" });
    static GenerationRequest Request(bool inline = true) => new() { ModelId = RunwaySmokePolicy.ModelId, Prompt = RunwaySmokePolicy.Prompt,
        Inputs = new() { new("firstFrame", inline ? "data:image/png;base64," + Convert.ToBase64String(Source()) : "", RunwaySmokePolicy.SourceAssetId) } };
    static GenerationJob Job()
    {
        var m = Model(); var v = GenerationPolicy.Validate(m, Request());
        return new() { Model = m, Prompt = v.Prompt, Inputs = v.Inputs, Settings = v.Settings, UserId = BflHomologationPolicy.OwnerId,
            RequestHash = GenerationPolicy.Fingerprint(m, v), Quote = GenerationPolicy.Quote(m, v, DateTime.UtcNow),
            RunwayRunId = RunwaySmokePolicy.RunId, SourceAssetId = RunwaySmokePolicy.SourceAssetId, IdempotencyKey = "runway-single-video-only-slot",
            Lease = "unit-lease", Status = GenerationStatus.Starting, DeadlineAt = DateTime.UtcNow.AddMinutes(10) };
    }
    sealed class Wire : HttpMessageHandler, IHttpClientFactory
    {
        public List<(string Method, string Url, string Version, string Body)> Requests = new();
        readonly Queue<HttpResponseMessage> replies;
        public Wire(params string[] json) => replies = new(json.Select(j => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(j, Encoding.UTF8, "application/json") }));
        public void Add(HttpResponseMessage response) => replies.Enqueue(response);
        public HttpClient CreateClient(string name) => new(this, false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add((request.Method.Method, request.RequestUri!.ToString(), request.Headers.TryGetValues("X-Runway-Version", out var versions) ? string.Join("", versions) : "",
                request.Content == null ? "" : await request.Content.ReadAsStringAsync(ct)));
            return replies.Dequeue();
        }
    }
    [Fact]
    public void ExactConfigUsesExistingFormulaAndKeepsOnlyRealCapabilities()
    {
        var job = Job(); RunwaySmokePolicy.ValidateJob(job, WithinAuthorization);
        Assert.Equal(0.16m, job.Quote.ProviderCostEstimateUsd); Assert.Equal(0.186m, job.Quote.ImaginoCostEstimateUsd); Assert.Equal(54, job.Quote.Credits);
        Assert.Equal(54, GenerationPolicy.CatalogStartingCredits(job.Model, WithinAuthorization));
        Assert.Equal(new[] { "imageToVideo", "firstFrame" }, job.Model.Capabilities);
        Assert.Single(job.Model.Inputs); Assert.True(job.Model.Inputs[0].Required); Assert.True(job.Model.Inputs[0].OwnedAssetOnly);
    }
    [Theory]
    [InlineData("duration", "6")][InlineData("resolution", "1080p")][InlineData("audio", "true")]
    public void UnsupportedDurationResolutionAudioFailBeforeDispatch(string key, string value)
    {
        var request = Request(); request.Settings[key] = key == "duration" ? JsonSerializer.SerializeToElement(int.Parse(value)) : JsonSerializer.SerializeToElement(value);
        Assert.Throws<ValidationAppException>(() => GenerationPolicy.Validate(Model(), request));
    }
    [Fact]
    public void MissingFirstFrameSecondFrameAndArbitraryImageAreRejected()
    {
        var r = Request(); r.Inputs.Clear(); Assert.Throws<ValidationAppException>(() => GenerationPolicy.Validate(Model(), r));
        r = Request(); r.Inputs.Add(new("lastFrame", r.Inputs[0].Data)); Assert.Throws<ValidationAppException>(() => GenerationPolicy.Validate(Model(), r));
        r = Request(); r.Inputs[0] = r.Inputs[0] with { SourceAssetId = null }; Assert.Throws<ValidationAppException>(() => GenerationPolicy.Validate(Model(), r));
    }
    [Fact]
    public void ScopeFingerprintAssetAndCostTamperingFailClosed()
    {
        foreach (var alter in new Action<GenerationJob>[] { j => j.UserId = "foreign", j => j.Prompt += " changed", j => j.SourceAssetId = "foreign-asset",
            j => j.RequestHash = "changed", j => j.Model.Version = "changed", j => j.Model.ProviderModel = "wan3", j => j.RunwayRunId = "other",
            j => j.Quote = j.Quote with { Credits = 1 }, j => j.Model.Pricing.ReferenceUsd = 0,
            j => j.Inputs[0] = j.Inputs[0] with { Data = "data:image/png;base64," + Convert.ToBase64String(new byte[34]) } })
        {
            var job = Job(); alter(job);
            Assert.ThrowsAny<Exception>(() => RunwaySmokePolicy.ValidateJob(job, WithinAuthorization));
        }
        Assert.Throws<ForbiddenFeatureException>(() => RunwaySmokePolicy.ValidateJob(Job(), RunwaySmokePolicy.ExpiresAtUtc));
    }
    [Theory]
    [InlineData("Reserved")][InlineData("SubmissionAttempted")][InlineData("Accepted")][InlineData("Completed")][InlineData("Failed")][InlineData("Cancelled")]
    public void DurableSlotCannotReopenAfterFailureTimeoutAmbiguityOrRestart(string state)
    {
        var l = new RunwaySmokeLedger(); Assert.True(RunwaySmokePolicy.CanReserve(l, WithinAuthorization));
        l.State = state; l.JobId = "persisted-job"; l.AttemptCount = 1;
        var resumed = BsonSerializer.Deserialize<RunwaySmokeLedger>(l.ToBson());
        Assert.False(RunwaySmokePolicy.CanReserve(resumed, WithinAuthorization)); Assert.Equal(1, resumed.AttemptCount);
        Assert.Equal(BsonType.Decimal128, resumed.ToBsonDocument()["BudgetUsd"].BsonType);
    }
    [Fact]
    public async Task WireMapsOneFirstFrameDurationResolutionVersionAndEstimatedCost()
    {
        var wire = new Wire("{\"id\":\"" + TaskId + "\",\"estimatedCost\":{\"credits\":16}}");
        var result = await new RunwayGenerationProvider(new(wire), Settings()).StartAsync(Job(), default);
        Assert.Equal(TaskId, result.JobId); Assert.Equal(0.16m, result.EstimatedCostUsd); Assert.Null(result.CostUsd);
        Assert.Equal("POST", wire.Requests.Single().Method); Assert.Equal(RunwayGenerationProvider.Endpoint, wire.Requests[0].Url);
        Assert.Equal("2024-11-06", wire.Requests[0].Version);
        using var body = JsonDocument.Parse(wire.Requests[0].Body); var root = body.RootElement;
        Assert.Equal(RunwaySmokePolicy.NativeModel, root.GetProperty("model").GetString()); Assert.Equal(5, root.GetProperty("duration").GetInt32());
        Assert.Equal("auto_720p", root.GetProperty("ratio").GetString()); Assert.Single(root.GetProperty("promptImage").EnumerateArray());
        Assert.Equal("first", root.GetProperty("promptImage")[0].GetProperty("position").GetString());
        Assert.Equal(5, root.EnumerateObject().Count());
    }
    [Theory]
    [InlineData("PENDING")][InlineData("THROTTLED")][InlineData("RUNNING")]
    public async Task PollPendingStatesRemainCanonicalProcessing(string state)
    {
        var wire = new Wire("{\"id\":\"" + TaskId + "\",\"status\":\"" + state + "\",\"estimatedCost\":{\"credits\":16}}");
        var job = Job(); job.ProviderJobId = TaskId; job.PollingUrl = RunwayGenerationProvider.Origin + "/v1/tasks/" + TaskId;
        var r = await new RunwayGenerationProvider(new(wire), Settings()).PollAsync(job, default);
        Assert.False(r.Completed); Assert.Null(r.ErrorCode); Assert.Equal(state, r.ProviderStatus); Assert.Equal("GET", wire.Requests.Single().Method);
    }
    [Theory]
    [InlineData("FAILED", "SAFETY.INPUT.IMAGE", "provider_moderated")][InlineData("FAILED", "INTERNAL", "provider_failed")]
    [InlineData("CANCELLED", "", "provider_cancelled")]
    public async Task TerminalErrorsCarryObservedCostAndNoRetry(string state, string failure, string code)
    {
        var wire = new Wire("{\"id\":\"" + TaskId + "\",\"status\":\"" + state + "\",\"failureCode\":\"" + failure + "\",\"cost\":{\"credits\":16}}");
        var job = Job(); job.ProviderJobId = TaskId; job.PollingUrl = RunwayGenerationProvider.Origin + "/v1/tasks/" + TaskId;
        var r = await new RunwayGenerationProvider(new(wire), Settings()).PollAsync(job, default);
        Assert.Equal(code, r.ErrorCode); Assert.Equal(0.16m, r.CostUsd); Assert.Single(wire.Requests);
    }
    [Fact]
    public async Task ForeignTaskAndUndocumentedOutputHostAreRejected()
    {
        var job = Job(); job.ProviderJobId = TaskId; job.PollingUrl = RunwayGenerationProvider.Origin + "/v1/tasks/" + TaskId;
        var wire = new Wire("{\"id\":\"foreign\",\"status\":\"RUNNING\"}");
        await Assert.ThrowsAsync<InvalidDataException>(() => new RunwayGenerationProvider(new(wire), Settings()).PollAsync(job, default));
        wire = new Wire("{\"id\":\"" + TaskId + "\",\"status\":\"SUCCEEDED\",\"cost\":{\"credits\":16},\"output\":[\"https://example.com/output.mp4\"]}");
        await Assert.ThrowsAnyAsync<Exception>(() => new RunwayGenerationProvider(new(wire), Settings()).PollAsync(job, default));
    }
    [Fact]
    public async Task OwnerSourceIsDownloadedAndBrowserSuppliedBytesAreIgnored()
    {
        var repo = new Mock<IGenerationRepository>(); var store = new Mock<IGenerationOutputStore>(); var job = Job();
        var source = new GenerationJob { Id = RunwaySmokePolicy.SourceAssetId, UserId = job.UserId, Status = GenerationStatus.Completed,
            CreditState = CreditState.Charged, Model = new() { MediaType = "image" } };
        repo.Setup(r => r.CatalogAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<GenerationModel> { job.Model });
        repo.Setup(r => r.GetAsync(source.Id, source.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(source);
        store.Setup(s => s.DownloadAsync(source, It.IsAny<CancellationToken>())).ReturnsAsync((Source(), "image/png"));
        var service = new GenerationService(repo.Object, new[] { new RunwayGenerationProvider(new(new Wire()), Settings()) }, Settings(), store.Object);
        var r = Request(false); var q = await service.QuoteAsync(r, default, job.UserId); Assert.Equal(54, q.Credits);
        await Assert.ThrowsAsync<ForbiddenFeatureException>(() => service.QuoteAsync(r, default, "foreign"));
        store.Verify(s => s.DownloadAsync(source, It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.ReserveAsync(It.IsAny<GenerationJob>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    [Fact]
    public void ClosedFlagBlocksRunwayAndOpenWindowBlocksAllOtherPaidModels()
    {
        var opts = Settings(); var provider = new RunwayGenerationProvider(new(new Wire()), opts);
        var service = new GenerationService(Mock.Of<IGenerationRepository>(), new[] { provider }, opts);
        Assert.Equal("ready", service.Availability(Model(), WithinAuthorization));
        foreach (var m in GenerationCatalog.Seed(true).Where(m => m.Provider is not ("runway" or "fixture"))) Assert.NotEqual("ready", service.Availability(m));
        opts.Value.RunwayRealSmokeEnabled = false; Assert.Equal("approval_required", service.Availability(Model()));
    }
    [Fact]
    public void OtherServicesProfilesAndSimultaneousSpendingGatesAreRejected()
    {
        var data = new Dictionary<string,string> { ["ASPNETCORE_ENVIRONMENT"] = "AIStaging", ["RENDER_SERVICE_ID"] = BflHomologationPolicy.ServiceId,
            ["RENDER_GIT_BRANCH"] = "codex/imagino-ai-revival-v2", ["RENDER_EXTERNAL_HOSTNAME"] = "imagino-api-ai-staging.onrender.com",
            ["GenerationV2:RunwayIntegrationEnabled"] = "true", ["GenerationV2:RunwayApiKey"] = "unit-only" };
        AIStagingConfiguration.Validate(AIStagingTests.Valid(data));
        data["RENDER_SERVICE_ID"] = "foreign"; Assert.Throws<InvalidOperationException>(() => AIStagingConfiguration.Validate(AIStagingTests.Valid(data)));
        data["RENDER_SERVICE_ID"] = BflHomologationPolicy.ServiceId; data["ASPNETCORE_ENVIRONMENT"] = "Production";
        Assert.Throws<InvalidOperationException>(() => GenerationRegistration.ValidateStaging(AIStagingTests.Valid(data)));
    }
    static (Mock<IGenerationRepository> Repo, Mock<IGenerationProvider> Provider, Mock<IGenerationOutputStore> Store, GenerationProcessor Processor) Processor(GenerationJob job)
    {
        var repo = new Mock<IGenerationRepository>(); var provider = new Mock<IGenerationProvider>(); var store = new Mock<IGenerationOutputStore>(); var opts = Settings();
        repo.Setup(r => r.CatalogAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<GenerationModel> { job.Model });
        provider.SetupGet(p => p.Name).Returns("runway"); provider.SetupGet(p => p.IsConfigured).Returns(true);
        return (repo, provider, store, new(repo.Object, new[] { provider.Object }, new(repo.Object, new[] { provider.Object }, opts),
            new(Mock.Of<IHttpClientFactory>()), store.Object, opts, NullLogger<GenerationProcessor>.Instance));
    }
    [Fact]
    public async Task ReceivedTaskBindingRetriesPersistenceOnly()
    {
        var job = Job(); var t = Processor(job);
        t.Repo.Setup(r => r.BeginRunwaySubmissionAsync(job, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var result = new ProviderResult(TaskId, RunwayGenerationProvider.Origin + "/v1/tasks/" + TaskId, EstimatedCostUsd: 0.16m);
        t.Provider.Setup(p => p.StartAsync(job, It.IsAny<CancellationToken>())).ReturnsAsync(result);
        t.Repo.SetupSequence(r => r.BindAsync(job, result, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException()).Returns(Task.CompletedTask);
        await t.Processor.ProcessAsync(job, default);
        t.Provider.Verify(p => p.StartAsync(job, It.IsAny<CancellationToken>()), Times.Once);
        t.Repo.Verify(r => r.BindAsync(job, result, It.IsAny<CancellationToken>()), Times.Exactly(2));
        t.Repo.Verify(r => r.SettleAsync(It.IsAny<GenerationJob>(), It.IsAny<GenerationStatus>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    [Fact]
    public async Task RateLimitDefersPollingAndHonorsRetryAfterWithoutCreation()
    {
        var job = Job(); job.Status = GenerationStatus.Processing; job.ProviderJobId = TaskId; var t = Processor(job);
        t.Provider.Setup(p => p.PollAsync(job, It.IsAny<CancellationToken>())).ThrowsAsync(new ProviderCallException(429, retryAfterSeconds: 30));
        await t.Processor.ProcessAsync(job, default);
        Assert.Equal(30, job.PollDelaySeconds);
        t.Repo.Verify(r => r.DeferAsync(job, true, It.IsAny<CancellationToken>()), Times.Once);
        t.Provider.Verify(p => p.StartAsync(It.IsAny<GenerationJob>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    [Theory]
    [InlineData("image/png", 1000)][InlineData("video/mp4", 10)]
    public async Task DownloadRejectsWrongMimeAndOversizedBytes(string mime, int limit)
    {
        var wire = new Wire(); var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Mp4()) };
        response.Content.Headers.ContentType = new(mime); wire.Add(response);
        await Assert.ThrowsAsync<InvalidDataException>(() => new GenerationProviderHttp(wire).DownloadAsync(
            "https://dnznrvs05pmza.cloudfront.net/result.mp4", RunwayGenerationProvider.OutputHosts, null, limit, default, "video/mp4"));
        Assert.Single(wire.Requests); Assert.Equal("GET", wire.Requests[0].Method);
    }
    [Fact]
    public async Task AmbiguousCreationAndSecondAttemptCanNeverPostTwice()
    {
        var job = Job(); var t = Processor(job);
        t.Repo.SetupSequence(r => r.BeginRunwaySubmissionAsync(job, It.IsAny<CancellationToken>())).ReturnsAsync(true).ReturnsAsync(false);
        t.Provider.Setup(p => p.StartAsync(job, It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException());
        await t.Processor.ProcessAsync(job, default); await t.Processor.ProcessAsync(job, default);
        t.Provider.Verify(p => p.StartAsync(job, It.IsAny<CancellationToken>()), Times.Once);
        t.Repo.Verify(r => r.SettleAsync(job, GenerationStatus.Failed, null, "submission_unknown", It.IsAny<CancellationToken>()), Times.Once);
    }
    [Fact]
    public async Task OutputInspectionRejectsForeignOpenGateAndUnusedSlotWithoutHttp()
    {
        var opts = Settings(); var wire = new Wire(); var repo = new Mock<IGenerationRepository>();
        var inspection = new RunwayOutputInspection(repo.Object, new(wire), opts);
        Assert.Null(await inspection.InspectAsync("foreign", default));
        Assert.Null(await inspection.InspectAsync(BflHomologationPolicy.OwnerId, default));
        opts.Value.PaidGenerationEnabled = false; opts.Value.RunwayRealSmokeEnabled = false;
        repo.Setup(r => r.RunwayLedgerAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new RunwaySmokeLedger());
        Assert.Null(await inspection.InspectAsync(BflHomologationPolicy.OwnerId, default));
        Assert.Empty(wire.Requests);
    }
    [Theory]
    [InlineData("application/octet-stream", "content_type_mismatch")]
    [InlineData("video/mp4", "valid_mp4")]
    public async Task OutputInspectionOnlyReadsPersistedFailedTaskAndNeverSettles(string mime, string expected)
    {
        var opts = Settings(); opts.Value.PaidGenerationEnabled = false; opts.Value.RunwayRealSmokeEnabled = false;
        var job = Job(); job.Status = GenerationStatus.Failed; job.CreditState = CreditState.Refunded;
        job.ProviderStatus = "SUCCEEDED"; job.ProviderJobId = TaskId;
        job.PollingUrl = RunwayGenerationProvider.Origin + "/v1/tasks/" + TaskId;
        var ledger = new RunwaySmokeLedger { State = "Failed", Halted = true, HaltReason = "invalid_provider_output",
            AttemptCount = 1, SettlementCount = 1, JobId = job.Id, TaskId = TaskId };
        var repo = new Mock<IGenerationRepository>(MockBehavior.Strict);
        repo.Setup(r => r.RunwayLedgerAsync(It.IsAny<CancellationToken>())).ReturnsAsync(ledger);
        repo.Setup(r => r.GetAsync(job.Id, job.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(job);
        var wire = new Wire("{\"id\":\""+TaskId+"\",\"status\":\"SUCCEEDED\",\"cost\":{\"credits\":16},\"output\":[\"https://dnznrvs05pmza.cloudfront.net/result.mp4\"]}");
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Mp4()) };
        response.Content.Headers.ContentType = new(mime); wire.Add(response);
        var result = await new RunwayOutputInspection(repo.Object, new(wire), opts).InspectAsync(job.UserId, default);
        Assert.Equal(expected, result!.Validation); Assert.Equal(mime, result.ContentType);
        Assert.Equal(2, wire.Requests.Count); Assert.All(wire.Requests, r => Assert.Equal("GET", r.Method));
        repo.Verify(r => r.RunwayLedgerAsync(It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.GetAsync(job.Id, job.UserId, It.IsAny<CancellationToken>()), Times.Once);
        repo.VerifyNoOtherCalls();
    }
    [Fact]
    public async Task TimeoutWithNoTaskNeverRestartsCreation()
    {
        var job = Job(); job.DeadlineAt = DateTime.UtcNow.AddSeconds(-1); var t = Processor(job);
        await t.Processor.ProcessAsync(job, default);
        t.Provider.Verify(p => p.StartAsync(It.IsAny<GenerationJob>(), It.IsAny<CancellationToken>()), Times.Never);
        t.Repo.Verify(r => r.SettleAsync(job, GenerationStatus.Failed, null, "submission_unknown", It.IsAny<CancellationToken>()), Times.Once);
    }
    [Fact]
    public async Task RestartAndDuplicatePollUseOnlyPersistedTask()
    {
        var job = BsonSerializer.Deserialize<GenerationJob>(Job().ToBson()); job.Status = GenerationStatus.Processing; job.ProviderJobId = TaskId;
        var t = Processor(job); t.Provider.Setup(p => p.PollAsync(job, It.IsAny<CancellationToken>())).ReturnsAsync(new ProviderResult(TaskId, null, ProviderStatus: "RUNNING"));
        await t.Processor.ProcessAsync(job, default); await t.Processor.ProcessAsync(job, default);
        t.Provider.Verify(p => p.StartAsync(It.IsAny<GenerationJob>(), It.IsAny<CancellationToken>()), Times.Never);
        t.Provider.Verify(p => p.PollAsync(job, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
    public static byte[] Mp4()
    {
        static byte[] Box(string type, params byte[][] parts) { var body = parts.SelectMany(p => p).ToArray(); var b = new byte[8 + body.Length];
            BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(0,4), (uint)b.Length); Encoding.ASCII.GetBytes(type).CopyTo(b,4); body.CopyTo(b,8); return b; }
        var mvhd = new byte[100]; BinaryPrimitives.WriteUInt32BigEndian(mvhd.AsSpan(12,4),1000); BinaryPrimitives.WriteUInt32BigEndian(mvhd.AsSpan(16,4),5000);
        var tkhd = new byte[84]; BinaryPrimitives.WriteUInt32BigEndian(tkhd.AsSpan(76,4),960u << 16); BinaryPrimitives.WriteUInt32BigEndian(tkhd.AsSpan(80,4),960u << 16);
        var hdlr = new byte[24]; Encoding.ASCII.GetBytes("vide").CopyTo(hdlr,8);
        return Box("ftyp", Encoding.ASCII.GetBytes("isom"), new byte[4]).Concat(Box("moov", Box("mvhd",mvhd),Box("trak",Box("tkhd",tkhd),Box("mdia",Box("hdlr",hdlr)))))
            .Concat(Box("mdat", new byte[] {1,2,3,4})).ToArray();
    }
    [Fact]
    public void Mp4ValidationRejectsEmptyTruncatedWrongContainerAndMissingMovie()
    {
        var m = GeneratedVideoValidator.Validate(Mp4()); Assert.Equal(5, m.DurationSeconds); Assert.Equal(960,m.Width);
        foreach (var invalid in new[] { Array.Empty<byte>(), new byte[40], Mp4()[..24], Mp4()[..^1] }) Assert.Throws<InvalidDataException>(() => GeneratedVideoValidator.Validate(invalid));
        var wrongResolution = Mp4();
        var track = Encoding.ASCII.GetString(wrongResolution).IndexOf("tkhd", StringComparison.Ordinal) + 4;
        BinaryPrimitives.WriteUInt32BigEndian(wrongResolution.AsSpan(track + 76, 4), 1080u << 16);
        BinaryPrimitives.WriteUInt32BigEndian(wrongResolution.AsSpan(track + 80, 4), 1080u << 16);
        Assert.Equal(1080, GeneratedVideoValidator.ReadMetadata(wrongResolution).Width);
        Assert.Throws<InvalidDataException>(() => GeneratedVideoValidator.Validate(wrongResolution));
    }
    [Theory]
    [InlineData(720,720)][InlineData(1280,720)][InlineData(960,720)][InlineData(1920,1080)]
    public void SquareAuto720pSmokeRejectsOtherGeometries(int width, int height)
    {
        var bytes = Mp4(); var track = Encoding.ASCII.GetString(bytes).IndexOf("tkhd", StringComparison.Ordinal) + 4;
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(track + 76, 4), (uint)width << 16);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(track + 80, 4), (uint)height << 16);
        Assert.Equal(width, GeneratedVideoValidator.ReadMetadata(bytes).Width);
        Assert.Throws<InvalidDataException>(() => GeneratedVideoValidator.Validate(bytes));
    }
    [Fact]
    public async Task StoredMp4PrecedesCompletedChargedAndStorageFailureRefunds()
    {
        var job = Job(); job.Status = GenerationStatus.Processing; job.ProviderJobId = TaskId; var t = Processor(job);
        t.Provider.Setup(p => p.PollAsync(job, It.IsAny<CancellationToken>())).ReturnsAsync(new ProviderResult(TaskId,null,Completed:true,Bytes:Mp4(),CostUsd:0.16m,ProviderStatus:"SUCCEEDED"));
        var stored = false;
        t.Store.Setup(s => s.StoreAsync(job, It.IsAny<byte[]>(), It.IsAny<CancellationToken>())).Callback(() => stored=true).ReturnsAsync("/api/generation/jobs/"+job.Id+"/download");
        t.Repo.Setup(r => r.SettleAsync(job, GenerationStatus.Completed, It.IsAny<string>(), null, It.IsAny<CancellationToken>())).Callback(() => Assert.True(stored)).ReturnsAsync(true);
        await t.Processor.ProcessAsync(job,default); Assert.Equal(5,job.OutputMetrics!.DurationSeconds); Assert.Equal(64,job.OutputMetrics.Sha256!.Length);
        t.Store.Setup(s => s.StoreAsync(job, It.IsAny<byte[]>(), It.IsAny<CancellationToken>())).ThrowsAsync(new IOException());
        await t.Processor.ProcessAsync(job,default);
        t.Repo.Verify(r => r.SettleAsync(job, GenerationStatus.Failed,null,"output_storage_failed",It.IsAny<CancellationToken>()),Times.Once);
        t.Repo.Verify(r => r.SettleAsync(job, GenerationStatus.Completed, It.IsAny<string>(),null,It.IsAny<CancellationToken>()),Times.Once);
    }
    [Fact]
    public async Task ObservedCostAboveCeilingAndInvalidOutputNeverCharge()
    {
        var job=Job(); job.Status=GenerationStatus.Processing;job.ProviderJobId=TaskId;var t=Processor(job);
        t.Provider.Setup(p=>p.PollAsync(job,It.IsAny<CancellationToken>())).ReturnsAsync(new ProviderResult(TaskId,null,Completed:true,Bytes:Mp4(),CostUsd:0.26m,ProviderStatus:"SUCCEEDED"));
        await t.Processor.ProcessAsync(job,default);
        t.Repo.Verify(r=>r.SettleAsync(job,GenerationStatus.Failed,null,"provider_cost_unverified_or_exceeded",It.IsAny<CancellationToken>()),Times.Once);
        t.Provider.Setup(p=>p.PollAsync(job,It.IsAny<CancellationToken>())).ReturnsAsync(new ProviderResult(TaskId,null,Completed:true,Bytes:new byte[20],CostUsd:0.16m,ProviderStatus:"SUCCEEDED"));
        await t.Processor.ProcessAsync(job,default);
        t.Repo.Verify(r=>r.SettleAsync(job,GenerationStatus.Failed,null,"invalid_provider_output",It.IsAny<CancellationToken>()),Times.Once);
        t.Store.Verify(s=>s.StoreAsync(It.IsAny<GenerationJob>(),It.IsAny<byte[]>(),It.IsAny<CancellationToken>()),Times.Never);
    }
}
