#nullable enable
using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Imagino.Api.DTOs;
using Imagino.Api.Models;
using Moq;
using Xunit;

namespace Imagino.Api.Tests;

public class AuthEmailFlowTests
{
    [Theory]
    [InlineData("resend-verification", "verify_email")]
    [InlineData("password/forgot", "reset_password")]
    public async Task UserRateLimitSendsThreeAndResponsesDoNotEnumerate(string route, string purpose)
    {
        using var factory = new SecurityApiFactory();
        var user = new User { Id = "email-user", Email = "controlled@example.test" };
        factory.UserRepository.Setup(r => r.GetByEmailAsync(user.Email)).ReturnsAsync(user);
        factory.EmailSender.Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), null)).ReturnsAsync(true);
        using var client = factory.CreateClient();
        for (var i = 0; i < 4; i++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/" + route, new { email = user.Email });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("", await response.Content.ReadAsStringAsync());
        }
        var unknown = await client.PostAsJsonAsync("/api/auth/" + route, new { email = "unknown@example.test" });
        Assert.Equal(HttpStatusCode.OK, unknown.StatusCode);
        Assert.Equal("", await unknown.Content.ReadAsStringAsync());
        factory.EmailTokens.Verify(r => r.CreateAsync(user.Id, purpose, It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<string>()), Times.Exactly(3));
        factory.EmailSender.Verify(s => s.SendAsync(user.Email, It.IsAny<string>(), It.IsAny<string>(), null), Times.Exactly(3));
    }

    [Theory]
    [InlineData("resend-verification")]
    [InlineData("password/forgot")]
    public async Task IpRateLimitBlocksEleventhSendAcrossUsers(string route)
    {
        using var factory = new SecurityApiFactory();
        factory.UserRepository.Setup(r => r.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((string email) => new User { Id = email, Email = email });
        factory.EmailSender.Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), null)).ReturnsAsync(true);
        using var client = factory.CreateClient();
        for (var i = 0; i < 11; i++)
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/" + route, new { email = $"mock-{i}@example.test" })).StatusCode);
        factory.EmailSender.Verify(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), null), Times.Exactly(10));
    }

    [Fact]
    public async Task RegisterSendsVerificationWithoutExposingCredentialData()
    {
        using var factory = new SecurityApiFactory();
        var user = new User { Id = "new-user", Email = "controlled@example.test", Username = "synthetic" };
        factory.Users.Setup(s => s.CreateAsync(It.IsAny<CreateUserDto>())).ReturnsAsync(user);
        factory.EmailSender.Setup(s => s.SendAsync(user.Email, "Confirme seu e-mail", It.IsAny<string>(), null)).ReturnsAsync(true);
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new { email = user.Email, password = "mock-password", username = user.Username });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("{\"message\":\"User created. Please verify your email.\"}", await response.Content.ReadAsStringAsync());
        factory.EmailTokens.Verify(r => r.CreateAsync(user.Id, "verify_email", It.Is<string>(t => t.Length >= 64), TimeSpan.FromMinutes(60), It.IsAny<string>()), Times.Once);
        factory.EmailSender.Verify(s => s.SendAsync(user.Email, "Confirme seu e-mail", It.Is<string>(html => html.Contains("/verify?token=")), null), Times.Once);
    }

    [Fact]
    public async Task VerificationAllowsLoginAndRejectsSequentialReplay()
    {
        using var factory = new SecurityApiFactory();
        factory.ExtraSettings["RefreshTokenCookie:Domain"] = "";
        var user = new User { Id = "email-user", Email = "controlled@example.test", PasswordHash = BCrypt.Net.BCrypt.HashPassword("mock-password") };
        EmailToken? token = new() { UserId = user.Id, ExpiresAt = DateTime.UtcNow.AddMinutes(10) };
        factory.UserRepository.Setup(r => r.GetByEmailAsync(user.Email)).ReturnsAsync(user);
        factory.UserRepository.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
        factory.EmailTokens.Setup(r => r.GetActiveByRawTokenAsync("verify_email", "mock-token")).ReturnsAsync(() => token);
        factory.EmailTokens.Setup(r => r.InvalidateByUserAsync(user.Id, "verify_email")).Callback(() => token = null).Returns(Task.CompletedTask);
        using var client = factory.CreateClient();
        var login = new { email = user.Email, password = "mock-password" };
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/auth/login", login)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/verify-email", new { token = "mock-token" })).StatusCode);
        Assert.True(user.EmailVerified);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/verify-email", new { token = "mock-token" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", login)).StatusCode);
        factory.UserRepository.Verify(r => r.MarkEmailVerifiedAsync(user.Id, It.IsAny<DateTime>()), Times.Once);
    }

    [Theory]
    [InlineData("verify-email", "verify_email", false)]
    [InlineData("verify-email", "verify_email", true)]
    [InlineData("password/reset", "reset_password", false)]
    [InlineData("password/reset", "reset_password", true)]
    public async Task InvalidOrExpiredTokenHasNoAccountEffects(string route, string purpose, bool expired)
    {
        using var factory = new SecurityApiFactory();
        factory.EmailTokens.Setup(r => r.GetActiveByRawTokenAsync(purpose, "mock-token"))
            .ReturnsAsync(expired ? new EmailToken { UserId = "email-user", ExpiresAt = DateTime.UtcNow.AddMinutes(-1) } : null);
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/" + route, new { token = "mock-token", newPassword = "mock-new-password" })).StatusCode);
        factory.UserRepository.Verify(r => r.MarkEmailVerifiedAsync(It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
        factory.UserRepository.Verify(r => r.SetPasswordHashAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        factory.RefreshTokens.Verify(r => r.DeleteByUserIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ResetChangesPasswordRevokesSessionsAndRejectsSequentialReplay()
    {
        using var factory = new SecurityApiFactory();
        factory.ExtraSettings["RefreshTokenCookie:Domain"] = "";
        var user = new User { Id = "email-user", Email = "controlled@example.test", EmailVerified = true, PasswordHash = BCrypt.Net.BCrypt.HashPassword("mock-old-password") };
        EmailToken? token = new() { UserId = user.Id, ExpiresAt = DateTime.UtcNow.AddMinutes(10) };
        factory.UserRepository.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
        factory.UserRepository.Setup(r => r.GetByEmailAsync(user.Email)).ReturnsAsync(user);
        factory.EmailTokens.Setup(r => r.GetActiveByRawTokenAsync("reset_password", "mock-reset")).ReturnsAsync(() => token);
        factory.EmailTokens.Setup(r => r.InvalidateByUserAsync(user.Id, "reset_password")).Callback(() => token = null).Returns(Task.CompletedTask);
        using var client = factory.CreateClient();
        var reset = new { token = "mock-reset", newPassword = "mock-new-password" };
        var response = await client.PostAsJsonAsync("/api/auth/password/reset", reset);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("", await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/password/reset", reset)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = "mock-old-password" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = "mock-new-password" })).StatusCode);
        factory.UserRepository.Verify(r => r.SetPasswordHashAsync(user.Id, It.Is<string>(h => BCrypt.Net.BCrypt.Verify("mock-new-password", h, false, BCrypt.Net.HashType.SHA384))), Times.Once);
        factory.RefreshTokens.Verify(r => r.DeleteByUserIdAsync(user.Id), Times.Once);
    }
}
