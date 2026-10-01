#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Imagino.Api.Models;
using Imagino.Api.Security;
using Imagino.Api.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace Imagino.Api.Tests;

public class GoogleOAuthSecurityTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
    public void GoogleConfigurationMustBeDisabledOrComplete(int configured)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ImageGeneratorSettings:MongoConnection"] = "mongodb://127.0.0.1:27017",
            ["ImageGeneratorSettings:MongoDatabase"] = "test",
            ["Jwt:Secret"] = "local-test-secret-with-at-least-32-bytes",
            ["Jwt:Issuer"] = "test", ["Jwt:Audience"] = "test",
            ["Frontend:BaseUrl"] = "https://app.test",
            ["RefreshTokenCookie:HttpOnly"] = "true", ["RefreshTokenCookie:Secure"] = "true",
            ["RefreshTokenCookie:SameSite"] = "None", ["RefreshTokenCookie:ExpiresDays"] = "7"
        };
        var keys = new[] { "Google:ClientId", "Google:ClientSecret", "Google:RedirectUri" };
        for (var i = 0; i < keys.Length; i++)
            if ((configured & (1 << i)) != 0) settings[keys[i]] = "synthetic-value-not-for-errors";
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        if (configured is 0 or 7) StartupConfiguration.Validate(config, false);
        else
        {
            var error = Assert.Throws<InvalidOperationException>(() => StartupConfiguration.Validate(config, false));
            Assert.Contains("Google:", error.Message);
            Assert.DoesNotContain("synthetic-value-not-for-errors", error.Message);
        }
    }

    [Fact]
    public async Task ValidStateWithoutBrowserCookieCannotExchangeCode()
    {
        using var factory = new SecurityApiFactory();
        using var client = Client(factory);
        var start = await client.GetAsync("/api/auth/google/login");
        var state = QueryHelpers.ParseQuery(start.Headers.Location!.Query)["state"];
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/auth/google/callback?code=fixture&state=" + state)).StatusCode);
        factory.Google.Verify(g => g.ExchangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        factory.RefreshTokens.Verify(r => r.CreateAsync(It.IsAny<RefreshToken>()), Times.Never);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task InvalidCodeOrUnverifiedIdentityCannotCreateUserOrSession(bool unverified)
    {
        using var factory = new SecurityApiFactory();
        factory.Google.Setup(g => g.ExchangeAsync("invalid-code", It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(unverified ? new GoogleIdentity("fixture-subject", "fixture@example.test", false) : null);
        using var client = Client(factory);
        var callback = await Begin(client, "invalid-code");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(callback)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(callback)).StatusCode);
        factory.Google.Verify(g => g.ExchangeAsync("invalid-code", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        factory.UserRepository.Verify(r => r.CreateAsync(It.IsAny<User>()), Times.Never);
        factory.RefreshTokens.Verify(r => r.CreateAsync(It.IsAny<RefreshToken>()), Times.Never);
    }

    [Fact]
    public async Task SameEmailLocalAccountRequiresExplicitLinkingAndNeverCreatesDuplicate()
    {
        using var factory = new SecurityApiFactory();
        factory.Google.Setup(g => g.ExchangeAsync("fixture", It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new GoogleIdentity("fixture-subject", "local@example.test", true));
        factory.UserRepository.Setup(r => r.GetByEmailAsync("local@example.test"))
            .ReturnsAsync(new User { Id = "local-user", Email = "local@example.test", GoogleId = null });
        using var client = Client(factory);
        var callback = await Begin(client, "fixture");
        var response = await client.GetAsync(callback);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("ACCOUNT_LINK_REQUIRED", await response.Content.ReadAsStringAsync());
        factory.UserRepository.Verify(r => r.CreateAsync(It.IsAny<User>()), Times.Never);
        factory.RefreshTokens.Verify(r => r.CreateAsync(It.IsAny<RefreshToken>()), Times.Never);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(callback)).StatusCode);
    }

    private static System.Net.Http.HttpClient Client(SecurityApiFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

    private static async Task<string> Begin(System.Net.Http.HttpClient client, string code)
    {
        var start = await client.GetAsync("/api/auth/google/login");
        Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
        client.DefaultRequestHeaders.Add("Cookie", start.Headers.GetValues("Set-Cookie").Single().Split(';')[0]);
        return "/api/auth/google/callback?code=" + code + "&state=" + QueryHelpers.ParseQuery(start.Headers.Location!.Query)["state"];
    }
}
