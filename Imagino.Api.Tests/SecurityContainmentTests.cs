using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Imagino.Api.Controllers;
using Imagino.Api.Controllers.Admin.Image;
using Imagino.Api.DTOs;
using Imagino.Api.Models;
using Imagino.Api.Models.Video;
using Imagino.Api.Repository;
using Imagino.Api.Security;
using Imagino.Api.Services;
using Imagino.Api.Services.Image;
using Imagino.Api.Services.Video;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Imagino.Api.Tests;

public class SecurityContainmentTests
{
    [Theory]
    [InlineData("credits", 1000000)]
    [InlineData("subscription", "Ultra")]
    public async Task Register_RejectsClientControlledEconomicFields(string field, object value)
    {
        using var factory = new SecurityApiFactory();
        using var client = factory.CreateClient();
        var body = new Dictionary<string, object>
        {
            ["email"] = "new@example.test",
            ["password"] = "test-password",
            [field] = value
        };

        var response = await client.PostAsJsonAsync("/api/auth/register", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        factory.Users.Verify(s => s.CreateAsync(It.IsAny<CreateUserDto>()), Times.Never);
    }

    [Fact]
    public async Task NormalUser_CannotAccessImageAdminRouteOrMutateVideoCatalog()
    {
        using var factory = new SecurityApiFactory();
        using var client = factory.ClientFor("user-a");

        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/admin/image/models")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync("/api/video/models", new { slug = "test" })).StatusCode);
    }

    [Fact]
    public async Task NormalUser_CannotIncrementCreditsOrCreateUser()
    {
        using var factory = new SecurityApiFactory();
        using var client = factory.ClientFor("user-a");

        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync("/api/users/user-b/credits", new { amount = 100 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync("/api/users", new { email = "b@example.test", password = "pw" })).StatusCode);
        factory.Users.Verify(s => s.IncrementCreditsAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task UserCannotReadOrUpdateOtherUser()
    {
        using var factory = new SecurityApiFactory();
        using var client = factory.ClientFor("user-a");

        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync("/api/users/user-b")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PutAsJsonAsync("/api/users/user-b", new { username = "stolen" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.DeleteAsync("/api/users/user-b")).StatusCode);
        factory.Users.Verify(s => s.GetByIdAsync("user-b"), Times.Never);
        factory.Users.Verify(s => s.UpdateAsync("user-b", It.IsAny<UserProfileUpdateDto>()), Times.Never);
        factory.Users.Verify(s => s.DeleteAsync("user-b"), Times.Never);
    }

    [Fact]
    public async Task UserCannotReadOtherUsersImageOrVideoJob()
    {
        using var factory = new SecurityApiFactory();
        using var client = factory.ClientFor("user-a");

        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync("/api/image/jobs/image-job")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync("/api/image/jobs/details/image-job")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync("/api/image/jobs/image-job/download")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync("/api/video/jobs/video-job")).StatusCode);
    }

    [Fact]
    public async Task AnonymousCannotDownloadImageJob()
    {
        using var factory = new SecurityApiFactory();
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/image/jobs/image-job/download")).StatusCode);
    }

    [Fact]
    public async Task PublicGalleryDoesNotExposePrivateJobs()
    {
        using var factory = new SecurityApiFactory();
        factory.ImageJobs.Setup(r => r.GetLatestAsync(12)).ReturnsAsync(new List<ImageJob>
        {
            new() { JobId = "private-image", UserId = "user-b", Prompt = "private prompt", ImageUrls = new() { "https://example.test/private.png" } }
        });
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/image/jobs/latest");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("[]", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task OwnerCanReadOwnImageAndVideoJobsAndProfile()
    {
        using var factory = new SecurityApiFactory();
        factory.ImageJobs.Setup(r => r.GetByJobIdAsync("own-image"))
            .ReturnsAsync(new ImageJob { JobId = "own-image", UserId = "user-a", Status = ImageJobStatus.Completed });
        factory.VideoJobs.Setup(r => r.GetByJobIdAsync("own-video"))
            .ReturnsAsync(new VideoJob { JobId = "own-video", UserId = "user-a", Status = VideoJobStatus.Completed });
        using var client = factory.ClientFor("user-a");

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/image/jobs/own-image")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/image/jobs/details/own-image")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/video/jobs/own-video")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/users/me")).StatusCode);
    }

    [Fact]
    public async Task ConfiguredAdminCanIncrementCredits()
    {
        using var factory = new SecurityApiFactory();
        factory.Users.Setup(s => s.IncrementCreditsAsync("user-b", 5)).ReturnsAsync(true);
        factory.Users.Setup(s => s.GetCreditsAsync("user-b")).ReturnsAsync(5);
        using var client = factory.ClientFor("admin-user");

        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync("/api/users/user-b/credits", new { amount = 5 })).StatusCode);
        factory.Users.Verify(s => s.IncrementCreditsAsync("user-b", 5), Times.Once);
    }

    [Fact]
    public async Task ProfileUpdateCannotPassEconomicFieldsToService()
    {
        using var factory = new SecurityApiFactory();
        factory.Users.Setup(s => s.UpdateAsync("user-a", It.IsAny<UserProfileUpdateDto>()))
            .ReturnsAsync(new User
            {
                Id = "user-a", Username = "new-name", Credits = 7,
                Email = "a@example.test", ProfileImageUrl = "https://example.test/original.png"
            });
        using var client = factory.ClientFor("user-a");

        var response = await client.PutAsJsonAsync("/api/users/me", new
        {
            username = "new-name", phoneNumber = "123", credits = 1000000,
            subscription = "Ultra", email = "attacker@example.test",
            password = "new-password", profileImageUrl = "https://example.test/attacker.png"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("a@example.test", json.RootElement.GetProperty("email").GetString());
        Assert.Equal("https://example.test/original.png", json.RootElement.GetProperty("profileImageUrl").GetString());
        Assert.Equal(7, json.RootElement.GetProperty("credits").GetInt32());
        factory.Users.Verify(s => s.UpdateAsync("user-a", It.Is<UserProfileUpdateDto>(
            dto => dto.Username == "new-name" && dto.PhoneNumber == "123")), Times.Once);
    }

    [Fact]
    public void PublicProfileUpdateDtoHasNoEconomicProperties()
    {
        var properties = typeof(UserProfileUpdateDto).GetProperties().Select(p => p.Name).ToArray();
        foreach (var forbidden in new[] { "Credits", "Subscription", "Plan", "SubscriptionStatus", "StripeCustomerId", "StripeSubscriptionId", "Email", "Password", "ProfileImageUrl" })
            Assert.DoesNotContain(forbidden, properties);
        Assert.Equal(new[] { "PhoneNumber", "Username" }, properties.OrderBy(p => p));
    }

    [Fact]
    public async Task AvatarUploadRequiresAuthenticationAndOwnership()
    {
        using var factory = new SecurityApiFactory();
        factory.Users.Setup(s => s.UpdateProfileImageAsync("user-a", It.IsAny<Microsoft.AspNetCore.Http.IFormFile>()))
            .ReturnsAsync("https://example.test/avatar.png");
        using var anonymous = factory.CreateClient();
        using var owner = factory.ClientFor("user-a");

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsync("/api/users/me/profile-image", AvatarForm())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await owner.PostAsync("/api/users/user-b/profile-image", AvatarForm())).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await owner.PostAsync("/api/users/me/profile-image", AvatarForm())).StatusCode);
        factory.Users.Verify(s => s.UpdateProfileImageAsync("user-a", It.IsAny<Microsoft.AspNetCore.Http.IFormFile>()), Times.Once);
        factory.Users.Verify(s => s.UpdateProfileImageAsync("user-b", It.IsAny<Microsoft.AspNetCore.Http.IFormFile>()), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublicVideoProvidersNeverExposeConfig(bool authenticated)
    {
        using var factory = new SecurityApiFactory();
        factory.VideoProviders.Setup(s => s.ListAsync()).ReturnsAsync(new List<VideoModelProvider>
        {
            new() { Name = "Video provider", Config = new Dictionary<string, string> { ["secret"] = "must-not-leak" } }
        });
        using var client = authenticated ? factory.ClientFor("user-a") : factory.CreateClient();

        var response = await client.GetAsync("/api/video/providers");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var provider = json.RootElement[0];
        Assert.Equal("Video provider", provider.GetProperty("name").GetString());
        Assert.False(provider.TryGetProperty("config", out _));
        Assert.DoesNotContain("must-not-leak", provider.ToString());
    }

    private static MultipartFormDataContent AvatarForm()
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(new byte[] { 1, 2, 3 });
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "File", "avatar.png");
        return form;
    }

    [Fact]
    public void AdminPolicyDeniesByDefaultAndUsesServerConfiguredIds()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim("sub", "user-a") }, "test"));
        var empty = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var configured = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Admin:UserIds:0"] = "user-a" }).Build();

        Assert.False(AdminAuthorization.IsConfiguredAdmin(principal, empty));
        Assert.True(AdminAuthorization.IsConfiguredAdmin(principal, configured));
    }

    [Fact]
    public void EveryCatalogMutationRequiresAdminPolicy()
    {
        var catalogControllers = new[]
        {
            typeof(ImageModelProvidersController), typeof(ImageModelsController),
            typeof(ImageModelVersionsController), typeof(ImageModelPresetsController),
            typeof(VideoModelProvidersController), typeof(VideoModelsController),
            typeof(VideoModelVersionsController), typeof(VideoModelPresetsController)
        };

        foreach (var controller in catalogControllers)
        {
            foreach (var method in controller.GetMethods())
            {
                if (!method.GetCustomAttributes(typeof(HttpPostAttribute), true).Any()
                    && !method.GetCustomAttributes(typeof(HttpPutAttribute), true).Any()
                    && !method.GetCustomAttributes(typeof(HttpDeleteAttribute), true).Any()
                    && !method.GetCustomAttributes(typeof(HttpPatchAttribute), true).Any()) continue;

                var attributes = method.GetCustomAttributes(typeof(AuthorizeAttribute), true)
                    .Concat(controller.GetCustomAttributes(typeof(AuthorizeAttribute), true))
                    .Cast<AuthorizeAttribute>();
                Assert.Contains(attributes, a => a.Policy == AdminAuthorization.Policy);
            }
        }
    }
}

internal sealed class SecurityApiFactory : WebApplicationFactory<Program>
{
    private const string Secret = "local-security-test-secret-with-32-plus-characters";
    private const string Issuer = "imagino-security-tests";
    private const string Audience = "imagino-security-tests";

    public Mock<IUserService> Users { get; } = new();
    public Mock<IUserRepository> UserRepository { get; } = new();
    public Mock<IRefreshTokenRepository> RefreshTokens { get; } = new();
    public Mock<IEmailTokenRepository> EmailTokens { get; } = new();
    public Mock<IEmailSender> EmailSender { get; } = new();
    public Mock<IImageJobRepository> ImageJobs { get; } = new();
    public Mock<IVideoJobRepository> VideoJobs { get; } = new();
    public Mock<IVideoModelProviderService> VideoProviders { get; } = new();
    public Mock<IGoogleOAuthClient> Google { get; } = new();
    public Mock<Imagino.Api.Services.Billing.IStripeBillingGateway> Stripe { get; } = new();
    public Mock<Imagino.Api.Repository.IStripeEventRepository> StripeEvents { get; } = new();
    public Dictionary<string, string> ExtraSettings { get; } = new();
    public Mock<Imagino.Api.Services.WebhookImage.IWebhookImageService> Webhooks { get; } = new();

    public SecurityApiFactory()
    {
        Users.Setup(s => s.GetByIdAsync("user-a"))
            .ReturnsAsync(new User { Id = "user-a", Email = "a@example.test", Username = "a" });
        ImageJobs.Setup(r => r.GetByJobIdAsync("image-job"))
            .ReturnsAsync(new ImageJob { JobId = "image-job", UserId = "user-b", Status = ImageJobStatus.Completed });
        VideoJobs.Setup(r => r.GetByJobIdAsync("video-job"))
            .ReturnsAsync(new VideoJob { JobId = "video-job", UserId = "user-b", Status = VideoJobStatus.Completed });
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting("Jwt:Secret", Secret);
        builder.UseSetting("Jwt:Issuer", Issuer);
        builder.UseSetting("Jwt:Audience", Audience);
        builder.UseSetting("ImageGeneratorSettings:MongoConnection", "mongodb://127.0.0.1:27017");
        builder.UseSetting("Admin:UserIds:0", "admin-user");
        builder.UseSetting("Google:ClientId", "test-client");
        builder.UseSetting("Google:ClientSecret", "test-secret");
        builder.UseSetting("Google:RedirectUri", "https://api.example.test/api/auth/google/callback");
        builder.UseSetting("Webhooks:ReplicateSigningSecret", "whsec_dGVzdC1zaWduaW5nLXNlY3JldA==");
        foreach (var setting in ExtraSettings) builder.UseSetting(setting.Key, setting.Value);
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IUserService>();
            services.RemoveAll<IUserRepository>();
            services.RemoveAll<IRefreshTokenRepository>();
            services.RemoveAll<IEmailTokenRepository>();
            services.RemoveAll<IEmailSender>();
            services.RemoveAll<IImageJobRepository>();
            services.RemoveAll<IVideoJobRepository>();
            services.RemoveAll<IVideoModelProviderService>();
            services.AddSingleton(Users.Object);
            services.AddSingleton(UserRepository.Object);
            services.AddSingleton(RefreshTokens.Object);
            services.AddSingleton(EmailTokens.Object);
            services.AddSingleton(EmailSender.Object);
            services.AddSingleton(ImageJobs.Object);
            services.AddSingleton(VideoJobs.Object);
            services.AddSingleton(VideoProviders.Object);
            services.RemoveAll<IGoogleOAuthClient>();
            services.RemoveAll<Imagino.Api.Services.WebhookImage.IWebhookImageService>();
            services.AddSingleton(Google.Object);
            services.AddSingleton(Webhooks.Object);
            services.RemoveAll<Imagino.Api.Services.Billing.IStripeBillingGateway>();
            services.RemoveAll<Imagino.Api.Repository.IStripeEventRepository>();
            services.AddSingleton(Stripe.Object);
            services.AddSingleton(StripeEvents.Object);
        });
    }

    public HttpClient ClientFor(string userId)
    {
        var client = CreateClient();
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = Secret,
                ["Jwt:Issuer"] = Issuer,
                ["Jwt:Audience"] = Audience
            }).Build();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", new JwtService(config).GenerateToken(userId, $"{userId}@example.test"));
        return client;
    }
}
