using System.Net;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using System.Text;
using System.Text.Json;
using Imagino.Api.Controllers;
using Imagino.Api.Services.Generation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using System.Security.Claims;

namespace Imagino.Api.Tests;

public sealed class GenerationProviderContractTests
{
    private sealed class Wire(params string[] responses) : HttpMessageHandler, IHttpClientFactory
    {
        public List<(string Method, string Url, string Body, string? Key)> Requests { get; } = new();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(ct);
            Requests.Add((request.Method.Method, request.RequestUri!.ToString(), body,
                request.Headers.TryGetValues("x-goog-api-key", out var google) ? google.Single() : request.Headers.GetValues("x-key").Single()));
            return new(HttpStatusCode.OK) { Content = new StringContent(responses[Requests.Count - 1], Encoding.UTF8, "application/json") };
        }
        public HttpClient CreateClient(string name) => new(this, false);
    }
    private static GenerationJob Job(string id) {
        var model = GenerationCatalog.Seed(false).Single(m => m.Id == id);
        return new() { Id = "012345678901234567890123", Model = model, Prompt = "private creative prompt",
            Settings = model.Fields.ToDictionary(f => f.Key, f => f.DefaultValue) };
    }
    private static IOptions<GenerationSettings> OptionsForTest() => Options.Create(new GenerationSettings { Enabled = true, BflApiKey = "unit-only", GeminiApiKey = "unit-only" });

    [Fact]
    public async Task BflUsesReturnedRegionalPollEndpointAndReportedCost()
    {
        var wire = new Wire("""{"id":"bound-1","polling_url":"https://api.us1.bfl.ai/v1/get_result?id=bound-1","cost":1.4}""",
            """{"status":"Ready","result":{"sample":"https://delivery.us1.bfl.ai/output.png"}}""");
        var provider = new BflGenerationProvider(new(wire), OptionsForTest()); var job = Job("flux-fast-20261002");
        var start = await provider.StartAsync(job, default); job.ProviderJobId = start.JobId; job.PollingUrl = start.PollingUrl;
        Assert.Equal(0.014m, start.CostUsd);
        var result = await provider.PollAsync(job, default);
        Assert.True(result.Completed); Assert.Contains("delivery.us1.bfl.ai", result.OutputUrl);
        Assert.Equal("https://api.bfl.ai/v1/flux-2-klein-4b", wire.Requests[0].Url);
        using var body = JsonDocument.Parse(wire.Requests[0].Body);
        Assert.Equal(1024, body.RootElement.GetProperty("width").GetInt32());
        Assert.Equal(start.PollingUrl, wire.Requests[1].Url); Assert.Equal("GET", wire.Requests[1].Method);
    }
    [Fact]
    public async Task BflNullableReportedCostStillBindsTheAcceptedJob()
    {
        var wire = new Wire("""{"id":"bound-1","polling_url":"https://api.us1.bfl.ai/v1/get_result?id=bound-1","cost":null}""");
        var result = await new BflGenerationProvider(new(wire), OptionsForTest()).StartAsync(Job("flux-fast-20261002"), default);
        Assert.Equal("bound-1", result.JobId); Assert.Null(result.CostUsd); Assert.Single(wire.Requests);
    }
    [Theory]
    [InlineData("1.4000000000000001", "0.014")]
    [InlineData("3.0000000000000004", "0.030")]
    [InlineData("4.500000000000001", "0.045")]
    [InlineData("3.001", "0.03001")]
    public async Task BflNormalizesWirePrecisionWithoutHidingPriceChanges(string credits, string expectedUsd)
    {
        var wire = new Wire("{\"id\":\"bound-1\",\"polling_url\":\"https://api.us1.bfl.ai/v1/get_result?id=bound-1\",\"cost\":" + credits + "}");
        var result = await new BflGenerationProvider(new(wire), OptionsForTest()).StartAsync(Job("flux-fast-20261002"), default);
        Assert.Equal(decimal.Parse(expectedUsd, System.Globalization.CultureInfo.InvariantCulture), result.CostUsd);
        Assert.Single(wire.Requests);
    }
    [Fact]
    public async Task GeminiBindsStoredInteractionAndReadsOnlyModelOutput()
    {
        var wire = new Wire("""{"id":"interaction-1","status":"completed"}""",
            """{"id":"interaction-1","status":"completed","steps":[{"type":"user_input","content":[{"type":"image","data":"AQ=="}]},{"type":"model_output","content":[{"type":"image","data":"AgM="}]}]}""");
        var provider = new GeminiImageGenerationProvider(new(wire), OptionsForTest()); var job = Job("gemini-edit-20261002");
        var start = await provider.StartAsync(job, default); job.ProviderJobId = start.JobId;
        var result = await provider.PollAsync(job, default);
        Assert.Equal(new byte[] { 2, 3 }, result.Bytes);
        using var body = JsonDocument.Parse(wire.Requests[0].Body);
        Assert.True(body.RootElement.GetProperty("store").GetBoolean());
        Assert.Equal("1K", body.RootElement.GetProperty("response_format").GetProperty("image_size").GetString());
        Assert.Equal("GET", wire.Requests[1].Method);
        Assert.EndsWith("/interactions/interaction-1", wire.Requests[1].Url);
    }
    [Fact]
    public async Task GeminiRejectsForeignInteractionResponse()
    {
        var wire = new Wire("""{"id":"someone-else","status":"completed"}""");
        var job = Job("gemini-edit-20261002"); job.ProviderJobId = "bound-1";
        await Assert.ThrowsAsync<InvalidDataException>(() => new GeminiImageGenerationProvider(new(wire), OptionsForTest()).PollAsync(job, default));
    }
    [Fact]
    public async Task VeoPreservesFirstAndLastFramesAndBoundOperation()
    {
        var wire = new Wire("""{"name":"models/veo-3.1-lite-generate-preview/operations/bound-1"}""",
            """{"name":"models/veo-3.1-lite-generate-preview/operations/bound-1","done":true,"response":{"generateVideoResponse":{"generatedSamples":[{"video":{"uri":"https://generativelanguage.googleapis.com/v1beta/files/output:download"}}]}}}""");
        var provider = new VeoGenerationProvider(new(wire), OptionsForTest()); var job = Job("veo-fast-20261002");
        job.Settings["duration"] = "8";
        job.Inputs = new() { new("firstFrame", "data:image/png;base64,AQ=="), new("lastFrame", "data:image/png;base64,Ag==") };
        var start = await provider.StartAsync(job, default); job.ProviderJobId = start.JobId;
        using var body = JsonDocument.Parse(wire.Requests[0].Body);
        Assert.Equal("AQ==", body.RootElement.GetProperty("instances")[0].GetProperty("image").GetProperty("inlineData").GetProperty("data").GetString());
        Assert.Equal("Ag==", body.RootElement.GetProperty("instances")[0].GetProperty("lastFrame").GetProperty("inlineData").GetProperty("data").GetString());
        Assert.Equal(8, body.RootElement.GetProperty("parameters").GetProperty("durationSeconds").GetInt32());
        Assert.True((await provider.PollAsync(job, default)).Completed);
    }
    [Fact]
    public async Task ForeignOwnerCannotReadOrDownloadJob()
    {
        var repository = new Mock<IGenerationRepository>(); var storage = new Mock<IGenerationOutputStore>();
        var options = OptionsForTest(); var service = new GenerationService(repository.Object, Array.Empty<IGenerationProvider>(), options);
        var controller = new GenerationController(repository.Object, service, storage.Object, options) {
            ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "foreign-owner") }, "test")) } } };
        Assert.IsType<NotFoundResult>(await controller.Get("012345678901234567890123", default));
        Assert.IsType<NotFoundResult>(await controller.Download("012345678901234567890123", default));
        repository.Verify(r => r.GetAsync("012345678901234567890123", "foreign-owner", It.IsAny<CancellationToken>()), Times.Exactly(2));
        storage.Verify(s => s.DownloadAsync(It.IsAny<GenerationJob>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
