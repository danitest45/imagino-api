#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Imagino.Api.Controllers;
using Imagino.Api.Services.Generation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace Imagino.Api.Tests;

public sealed class OpenAiPreflightTests
{
    private static GenerationSettings Settings() => new() { Enabled = true, OpenAiHomologationEnabled = true,
        PaidGenerationEnabled = false, OpenAiApiKey = "mock-preflight-key" };
    private static Dictionary<string, string?> Configuration() => new() {
        ["ASPNETCORE_ENVIRONMENT"] = "AIStaging", ["RENDER_SERVICE_ID"] = BflHomologationPolicy.ServiceId,
        ["RENDER_GIT_BRANCH"] = "codex/imagino-ai-revival-v2", ["RENDER_EXTERNAL_HOSTNAME"] = "imagino-api-ai-staging.onrender.com" };
    private sealed class Wire(HttpStatusCode status = HttpStatusCode.OK, string? body = null, bool timeout = false)
        : HttpMessageHandler, IHttpClientFactory
    {
        public List<string> Paths { get; } = new();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("api.openai.com", request.RequestUri!.Host);
            Assert.Null(request.Content);
            Assert.Equal("Bearer mock-preflight-key", request.Headers.GetValues("Authorization").Single());
            Paths.Add(request.RequestUri.AbsolutePath);
            if (timeout) throw new TaskCanceledException("mock-preflight-key raw-error-must-not-escape");
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body ?? JsonSerializer.Serialize(new {
                id = request.RequestUri.Segments.Last(), @object = "model", private_field = "raw-error-must-not-escape" })) });
        }
        public HttpClient CreateClient(string name) => new(this, false);
    }
    private static OpenAiModelAccessPreflight Probe(Wire wire, GenerationSettings? settings = null,
        Dictionary<string, string?>? configuration = null) => new(new(wire), Options.Create(settings ?? Settings()),
            new ConfigurationBuilder().AddInMemoryCollection(configuration ?? Configuration()).Build());

    [Fact]
    public async Task ChecksOnlyTheTwoFixedMetadataEndpointsAndDoesNotClaimImageAccess()
    {
        var wire = new Wire();
        var report = await Probe(wire).CheckAsync(BflHomologationPolicy.OwnerId, default);
        Assert.NotNull(report); Assert.Equal("metadata_only", report.Scope);
        Assert.False(report.ImageEndpointAccessVerified); Assert.False(report.CostBoundsVerified);
        Assert.Equal(new[] { "/v1/models/" + OpenAiHomologationPolicy.Flare, "/v1/models/" + OpenAiHomologationPolicy.Sunburst }, wire.Paths);
        Assert.All(report.Models, m => { Assert.Equal(200, m.HttpStatus); Assert.Equal("metadata_access_confirmed", m.Status); });
        var json = JsonSerializer.Serialize(report);
        Assert.DoesNotContain("mock-preflight-key", json); Assert.DoesNotContain("raw-error", json); Assert.DoesNotContain("private_field", json);
        Assert.NotNull(typeof(GenerationController).GetMethod(nameof(GenerationController.OpenAiPreflight))!.GetCustomAttribute<AuthorizeAttribute>());
    }
    [Theory]
    [InlineData("ASPNETCORE_ENVIRONMENT", "Production")]
    [InlineData("RENDER_SERVICE_ID", "old-service")]
    [InlineData("RENDER_GIT_BRANCH", "main")]
    [InlineData("RENDER_EXTERNAL_HOSTNAME", "production.example")]
    public async Task WrongRuntimeScopeHasNoNetworkAccess(string key, string value)
    {
        var wire = new Wire(); var config = Configuration(); config[key] = value;
        Assert.Null(await Probe(wire, configuration: config).CheckAsync(BflHomologationPolicy.OwnerId, default));
        Assert.Empty(wire.Paths);
    }
    [Theory]
    [InlineData("disabled")]
    [InlineData("authorization_closed")]
    [InlineData("paid_enabled")]
    [InlineData("foreign_owner")]
    public async Task OwnerAndClosedPaidGateAreRequiredBeforeSendingMetadataRequests(string condition)
    {
        var wire = new Wire(); var settings = Settings();
        if (condition == "disabled") settings.Enabled = false;
        if (condition == "authorization_closed") settings.OpenAiHomologationEnabled = false;
        if (condition == "paid_enabled") settings.PaidGenerationEnabled = true;
        Assert.Null(await Probe(wire, settings).CheckAsync(condition == "foreign_owner" ? "foreign" : BflHomologationPolicy.OwnerId, default));
        Assert.Empty(wire.Paths);
    }
    [Fact]
    public async Task MissingCredentialIsReportedWithoutSendingAnything()
    {
        var wire = new Wire(); var settings = Settings(); settings.OpenAiApiKey = "";
        var report = await Probe(wire, settings).CheckAsync(BflHomologationPolicy.OwnerId, default);
        Assert.Empty(wire.Paths); Assert.All(report!.Models, m => { Assert.Null(m.HttpStatus); Assert.Equal("credential_missing", m.Status); });
    }
    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(429)]
    [InlineData(500)]
    public async Task ProviderErrorsReturnOnlyTheStatusAndNeverRetryOrExposeTheBody(int status)
    {
        var wire = new Wire((HttpStatusCode)status, "mock-preflight-key raw-error-must-not-escape");
        var report = await Probe(wire).CheckAsync(BflHomologationPolicy.OwnerId, default);
        Assert.Equal(2, wire.Paths.Count); Assert.Equal(2, wire.Paths.Distinct().Count());
        Assert.All(report!.Models, m => { Assert.Equal(status, m.HttpStatus); Assert.Equal("provider_http_error", m.Status); });
        Assert.DoesNotContain("mock-preflight-key", JsonSerializer.Serialize(report)); Assert.DoesNotContain("raw-error", JsonSerializer.Serialize(report));
    }
    [Theory]
    [InlineData("not-json")]
    [InlineData("{\"id\":\"other-model\",\"object\":\"model\"}")]
    [InlineData("{\"id\":true,\"object\":\"model\"}")]
    public async Task InvalidMetadataCannotConfirmAccess(string body)
    {
        var wire = new Wire(body: body);
        var report = await Probe(wire).CheckAsync(BflHomologationPolicy.OwnerId, default);
        Assert.All(report!.Models, m => Assert.Equal("invalid_response", m.Status)); Assert.Equal(2, wire.Paths.Count);
    }
    [Fact]
    public async Task TimeoutReturnsOnlyItsCategoryAndDoesNotRetryTheModel()
    {
        var wire = new Wire(timeout: true);
        var report = await Probe(wire).CheckAsync(BflHomologationPolicy.OwnerId, default);
        Assert.All(report!.Models, m => { Assert.Null(m.HttpStatus); Assert.Equal("timeout", m.Status); });
        Assert.Equal(2, wire.Paths.Count); Assert.Equal(2, wire.Paths.Distinct().Count());
        Assert.DoesNotContain("mock-preflight-key", JsonSerializer.Serialize(report));
    }
}
