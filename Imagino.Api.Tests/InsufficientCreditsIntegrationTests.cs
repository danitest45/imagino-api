using System.Threading.Tasks;
using System.Collections.Generic;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Imagino.Api.Errors.Exceptions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Imagino.Api.Tests;

public class InsufficientCreditsIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public InsufficientCreditsIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration(c =>
            {
                var dict = new Dictionary<string, string>
                {
                    ["Jwt:Secret"] = "secretsecretsecretsecret",
                    ["Jwt:Issuer"] = "test",
                    ["Jwt:Audience"] = "test"
                };
                c.AddInMemoryCollection(dict!);
            });
            builder.ConfigureServices(services =>
            {
                services.AddControllers().AddApplicationPart(typeof(ThrowController).Assembly);
            });
        }).CreateClient();
    }

    [Fact]
    public async Task ReturnsProblemDetails()
    {
        var response = await _client.GetAsync("/throw");
        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("INSUFFICIENT_CREDITS", root.GetProperty("code").GetString());
        var meta = root.GetProperty("meta");
        Assert.Equal(1, meta.GetProperty("current").GetInt32());
        Assert.Equal(5, meta.GetProperty("needed").GetInt32());
    }
}

public class ThrowController : ControllerBase
{
    [HttpGet("/throw")]
    public IActionResult Get() => throw new InsufficientCreditsException(1, 5);
}
