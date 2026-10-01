#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Imagino.Api.DTOs;
using Imagino.Api.Errors;
using Imagino.Api.Models;
using Imagino.Api.Repository;
using Imagino.Api.Security;
using Imagino.Api.Services;
using Imagino.Api.Services.Storage;
using Imagino.Api.Services.WebhookImage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Moq;
using Xunit;

namespace Imagino.Api.Tests;

public class OperationalSecurityTests
{
    [Fact]
    public void OAuthStateIsBrowserBoundAndSingleUse()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var state = new GoogleOAuthState(cache);
        var tx = state.Begin();
        Assert.True(tx.State.Length >= 43);
        Assert.NotEqual(tx.State, state.Begin().State);
        Assert.Null(state.Consume(tx.State, "wrong-browser"));
        Assert.NotNull(state.Consume(tx.State, tx.BrowserToken));
        Assert.Null(state.Consume(tx.State, tx.BrowserToken));
        Assert.Equal("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM",
            TokenSecurity.PkceChallenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"));
    }

    [Fact]
    public void ExpiredOAuthStateFailsClosed()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var tx = new GoogleOAuthTransaction("expired", "browser", "nonce", "verifier", DateTimeOffset.UtcNow.AddMinutes(-1));
        cache.Set("oauth:expired", tx);
        Assert.Null(new GoogleOAuthState(cache).Consume("expired", "browser"));
    }

    [Fact]
    public void OAuthNonceMustMatchValidatedToken()
    {
        var claims = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode("{\"nonce\":\"expected\"}");
        Assert.True(GoogleOAuthClient.HasExpectedNonce("header." + claims + ".signature", "expected"));
        Assert.False(GoogleOAuthClient.HasExpectedNonce("header." + claims + ".signature", "wrong"));
    }

    [Theory]
    [InlineData("/api/auth/google/callback?code=forged")]
    [InlineData("/api/auth/google/callback?code=forged&state=forged")]
    public async Task OAuthCallbackRejectsMissingOrUnboundState(string url)
    {
        using var factory = new SecurityApiFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(url)).StatusCode);
        factory.Google.Verify(g => g.ExchangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task OAuthRedirectHasNoJwtAndReplayCannotIssueAnotherSession()
    {
        using var factory = new SecurityApiFactory();
        factory.Google.Setup(g => g.ExchangeAsync("code", It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new GoogleIdentity("google-subject", "a@example.test", true));
        factory.UserRepository.Setup(r => r.GetByGoogleIdAsync("google-subject"))
            .ReturnsAsync(new User { Id = "user-a", Email = "a@example.test", EmailVerified = true });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        var start = await client.GetAsync("/api/auth/google/login");
        Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
        var location = start.Headers.Location!;
        var query = QueryHelpers.ParseQuery(location.Query);
        Assert.Equal("S256", query["code_challenge_method"].ToString());
        Assert.False(string.IsNullOrWhiteSpace(query["nonce"]));
        var cookie = start.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
        Assert.Contains("httponly", start.Headers.GetValues("Set-Cookie").Single().ToLowerInvariant());
        Assert.Contains("secure", start.Headers.GetValues("Set-Cookie").Single().ToLowerInvariant());
        Assert.StartsWith("__Host-googleOAuth=", cookie);
        Assert.DoesNotContain("domain=", start.Headers.GetValues("Set-Cookie").Single().ToLowerInvariant());
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        var callback = "/api/auth/google/callback?code=code&state=" + query["state"];
        var response = await client.GetAsync(callback);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/google-auth", response.Headers.Location!.AbsolutePath);
        Assert.Equal("", response.Headers.Location.Query);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("refreshToken=") && c.Contains("httponly"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(callback)).StatusCode);
        factory.RefreshTokens.Verify(r => r.CreateAsync(It.IsAny<RefreshToken>()), Times.Once);
    }

    [Fact]
    public async Task RefreshUsesAtomicConsumptionAndSecureCookie()
    {
        using var factory = new SecurityApiFactory();
        factory.RefreshTokens.SetupSequence(r => r.ConsumeAsync("legacy-token"))
            .ReturnsAsync(new RefreshToken { UserId = "user-a", ExpiresAt = DateTime.UtcNow.AddHours(1) })
            .ReturnsAsync((RefreshToken?)null);
        factory.UserRepository.Setup(r => r.GetByIdAsync("user-a"))
            .ReturnsAsync(new User { Id = "user-a", Email = "a@example.test" });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Add("Cookie", "refreshToken=legacy-token");
        var response = await client.PostAsync("/api/auth/refresh", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cookie = response.Headers.GetValues("Set-Cookie").Single();
        Assert.Contains("httponly", cookie); Assert.Contains("secure", cookie); Assert.Contains("path=/", cookie);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/auth/refresh", null)).StatusCode);
        factory.RefreshTokens.Verify(r => r.CreateAsync(It.IsAny<RefreshToken>()), Times.Once);
    }

    [Fact]
    public void RefreshLookupSupportsLegacyButHashIsPreferred()
    {
        const string raw = "random-test-token";
        var filter = Render(RefreshTokenRepository.TokenFilter(raw));
        Assert.Contains(TokenSecurity.Hash(raw), filter.ToJson());
        Assert.NotEqual(raw, TokenSecurity.Hash(raw));
        Assert.Equal(64, TokenSecurity.Hash(raw).Length);
        Assert.Contains("TokenHash", filter.ToJson());
        Assert.Contains("Token", filter.ToJson());
        var stored = RefreshTokenRepository.ForStorage(new RefreshToken { UserId = "507f1f77bcf86cd799439011", Token = raw });
        Assert.Null(stored.Token);
        Assert.DoesNotContain(raw, stored.ToBsonDocument().ToJson());
        Assert.False(stored.ToBsonDocument().Contains("Token"));
    }

    [Fact]
    public async Task OptionalRotationCutoffRejectsLegacyRefreshSessions()
    {
        using var factory = new SecurityApiFactory();
        factory.ExtraSettings["Auth:RefreshTokensValidAfter"] = DateTime.UtcNow.ToString("O");
        factory.RefreshTokens.Setup(r => r.ConsumeAsync("legacy-token"))
            .ReturnsAsync(new RefreshToken { UserId = "user-a", ExpiresAt = DateTime.UtcNow.AddDays(1) });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Add("Cookie", "refreshToken=legacy-token");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/auth/refresh", null)).StatusCode);
        factory.RefreshTokens.Verify(r => r.CreateAsync(It.IsAny<RefreshToken>()), Times.Never);
    }

    [Theory]
    [InlineData("http://replicate.delivery/file")]
    [InlineData("https://127.0.0.1/file")]
    [InlineData("https://169.254.169.254/latest/meta-data")]
    [InlineData("https://localhost/file")]
    [InlineData("https://replicate.delivery.evil.test/file")]
    [InlineData("https://replicate.delivery@evil.test/file")]
    [InlineData("https://replicate.delivery:8443/file")]
    public void UnsafeRemoteUrlsAreRejected(string url) => Assert.Throws<ArgumentException>(() =>
        RemoteUrlPolicy.Validate(url, "replicate.delivery", "*.replicate.delivery"));

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.1.1")]
    [InlineData("172.16.0.1")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.169.254")]
    [InlineData("100.100.100.200")]
    [InlineData("::1")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("fd00::1")]
    [InlineData("fe80::1")]
    public void PrivateDnsResultsAreRejected(string ip) => Assert.False(RemoteUrlPolicy.IsPublicAddress(IPAddress.Parse(ip)));

    [Fact]
    public void PublicUrlsAndAddressesAreAccepted()
    {
        Assert.Equal("cdn.replicate.delivery", RemoteUrlPolicy.Validate("https://cdn.replicate.delivery/image.png", "*.replicate.delivery").Host);
        Assert.True(RemoteUrlPolicy.IsPublicAddress(IPAddress.Parse("8.8.8.8")));
        Assert.True(RemoteUrlPolicy.IsPublicAddress(IPAddress.Parse("2606:4700:4700::1111")));
        using var handler = RemoteUrlPolicy.CreateHandler();
        Assert.False(handler.AllowAutoRedirect); Assert.False(handler.UseProxy); Assert.NotNull(handler.ConnectCallback);
    }

    [Fact]
    public async Task DownloaderRejectsRedirectAndDoesNotSendGoogleKeyToStorage()
    {
        var handler = new StubHandler(req => {
            Assert.False(req.Headers.Contains("x-goog-api-key"));
            return new HttpResponseMessage(HttpStatusCode.Redirect) { Headers = { Location = new Uri("http://localhost/private") } };
        });
        var clients = new Mock<IHttpClientFactory>(); clients.Setup(c => c.CreateClient("ProviderSecure")).Returns(new HttpClient(handler));
        await Assert.ThrowsAsync<HttpRequestException>(() => new SafeMediaDownloader(clients.Object).DownloadAsync(
            "https://storage.googleapis.com/video", new[] { "storage.googleapis.com" }, 100, "test-google-key"));
    }

    [Fact]
    public async Task DownloaderRejectsOversizedOutput()
    {
        var clients = new Mock<IHttpClientFactory>();
        clients.Setup(c => c.CreateClient("ProviderSecure")).Returns(new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[11]) })));
        await Assert.ThrowsAsync<ArgumentException>(() => new SafeMediaDownloader(clients.Object).DownloadAsync(
            "https://replicate.delivery/file", new[] { "replicate.delivery" }, 10));
    }

    [Theory]
    [InlineData("<svg></svg>")]
    [InlineData("<html>bad</html>")]
    [InlineData("MZ executable")]
    public async Task AvatarRejectsSpoofedMimeAndExtension(string text)
    {
        var storage = new Mock<IStorageService>(); var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByIdAsync("owner")).ReturnsAsync(new User { Id = "owner" });
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        var file = new FormFile(stream, 0, stream.Length, "File", "avatar.png") { Headers = new HeaderDictionary(), ContentType = "image/png" };
        await Assert.ThrowsAsync<ArgumentException>(() => new UserService(repo.Object, storage.Object).UpdateProfileImageAsync("owner", file));
        storage.Verify(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AvatarLimitsActualStreamAndDerivesServerExtension()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => AvatarValidator.ReadAsync(new MemoryStream(new byte[AvatarValidator.MaxBytes + 1]), 1));
        await Assert.ThrowsAsync<ArgumentException>(() => AvatarValidator.ReadAsync(new MemoryStream(), AvatarValidator.MaxBytes + 1));
        Assert.Equal((".png", "image/png"), AvatarValidator.Identify(new byte[] {137,80,78,71,13,10,26,10}));
        Assert.Equal((".jpg", "image/jpeg"), AvatarValidator.Identify(new byte[] {255,216,255,1}));
        Assert.Equal((".webp", "image/webp"), AvatarValidator.Identify(Encoding.ASCII.GetBytes("RIFFxxxxWEBP")));
        var repo = new Mock<IUserRepository>(); repo.Setup(r => r.GetByIdAsync("owner")).ReturnsAsync(new User { Id = "owner" });
        var storage = new Mock<IStorageService>();
        storage.Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), "image/jpeg", It.IsAny<CancellationToken>())).ReturnsAsync("https://storage.test/avatar.jpg");
        using var stream = new MemoryStream(new byte[] {255,216,255,1});
        var file = new FormFile(stream,0,stream.Length,"File","../../executable.exe") { Headers = new HeaderDictionary(), ContentType="application/octet-stream" };
        await new UserService(repo.Object,storage.Object).UpdateProfileImageAsync("owner",file);
        storage.Verify(s => s.UploadAsync(It.IsAny<Stream>(), It.Is<string>(n => n.StartsWith("profile-images/") && n.EndsWith(".jpg") && !n.Contains("..")), "image/jpeg", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ReplicateSignatureRejectsTamperingExpiryAndMissingMetadata()
    {
        var now = DateTimeOffset.UtcNow; var timestamp = now.ToUnixTimeSeconds().ToString(); var body = Encoding.UTF8.GetBytes("{\"id\":\"prediction\"}");
        var secret = "whsec_" + Convert.ToBase64String(Encoding.UTF8.GetBytes("test-signing-secret-32bytes-long!!"));
        var signature = Sign(body, "message", timestamp, secret);
        Assert.True(WebhookVerifier.Verify(body,"message",timestamp,signature,secret,now));
        Assert.False(WebhookVerifier.Verify(Encoding.UTF8.GetBytes("{}"),"message",timestamp,signature,secret,now));
        Assert.False(WebhookVerifier.Verify(body,"message",timestamp,signature,secret,now.AddMinutes(6)));
        Assert.False(WebhookVerifier.Verify(body,"message",timestamp,signature,secret,now.AddMinutes(-6)));
        Assert.False(WebhookVerifier.Verify(body,null,timestamp,signature,secret,now));
        Assert.False(WebhookVerifier.Verify(body,"message",timestamp,"v1,invalid",secret,now));
    }

    [Fact]
    public async Task WebhooksFailClosedBeforeProcessing()
    {
        using var factory = new SecurityApiFactory(); using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsync("/api/webhooks/replicate",new StringContent("{}"))).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable,(await client.PostAsync("/api/webhooks/runpod",new StringContent("{}"))).StatusCode);
        factory.Webhooks.Verify(s=>s.ProcessarWebhookReplicateAsync(It.IsAny<ReplicateWebhookRequest>()),Times.Never);
    }

    [Fact]
    public async Task CompletedWebhookIsIdempotentAndCannotDebitAgain()
    {
        var job = new ImageJob { Id="507f1f77bcf86cd799439011", ProviderJobId="prediction", UserId="owner", CallbackProvider="Replicate",TokenConsumed=true,Status=ImageJobStatus.Completed };
        var jobs=new Mock<IImageJobRepository>();jobs.Setup(r=>r.GetByProviderJobIdAsync("prediction")).ReturnsAsync(job);
        var users=new Mock<IUserRepository>();users.Setup(r=>r.GetByIdAsync("owner")).ReturnsAsync(new User());
        var storage=new Mock<IStorageService>();
        var service=new WebhookImageService(jobs.Object,users.Object,storage.Object,new SafeMediaDownloader(new Mock<IHttpClientFactory>().Object));
        var result=await service.ProcessarWebhookReplicateAsync(new ReplicateWebhookRequest {Id="prediction",Status="succeeded",Output="http://localhost/bad"});
        Assert.Equal("Completed",result.Status);
        jobs.Verify(r=>r.TryClaimWebhookAsync(It.IsAny<ImageJob>(),It.IsAny<string>()),Times.Never);
        users.Verify(r=>r.DecrementCreditsAsync(It.IsAny<string>(),It.IsAny<int>()),Times.Never);
        job.CallbackProvider="GoogleGemini";
        await Assert.ThrowsAsync<ValidationAppException>(()=>service.ProcessarWebhookReplicateAsync(new ReplicateWebhookRequest {Id="prediction",Status="succeeded"}));
    }

    [Fact]
    public void AtomicUpdatesDoNotOverwriteUnrelatedUserFields()
    {
        var user=new User {Id="507f1f77bcf86cd799439011",Credits=999,Username="new",Email="private",StripeCustomerId="customer"};
        foreach(var update in new[] {UserUpdates.Billing(user),UserUpdates.VerifyEmail(DateTime.UtcNow),UserUpdates.Password("hash"),UserUpdates.Customer("customer")})
        {
            var json=update.Render(new RenderArgs<User>(BsonSerializer.SerializerRegistry.GetSerializer<User>(),BsonSerializer.SerializerRegistry)).ToJson();
            Assert.Contains("$set",json);
            foreach(var field in new[] {"Credits","Subscription\"","Username","ProfileImageUrl","PhoneNumber","Email\""}) Assert.DoesNotContain(field,json);
        }
        var filter=Render(ImageJobRepository.WebhookClaimFilter(new ImageJob {Id=user.Id,UserId="owner",ProviderJobId="prediction",CallbackProvider="Replicate"},DateTime.UtcNow)).ToJson();
        Assert.Contains("providerJobId",filter); Assert.Contains("userId",filter); Assert.Contains("tokenConsumed",filter); Assert.DoesNotContain("Completed",filter);
        Assert.False(WebhookImageService.CanComplete(ImageJobStatus.Failed));
        Assert.False(WebhookImageService.CanComplete(ImageJobStatus.Completed));
        var creditFilter = Render(UserRepository.BillingCreditFilter(user.Id!, "event")).ToJson();
        Assert.Contains("$ne", creditFilter); Assert.Contains("BillingCreditEvents", creditFilter);
    }

    [Fact]
    public void ConfigurationErrorsNeverIncludeValuesAndCorsUsesExactOrigins()
    {
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Jwt:Secret"]="short-do-not-print" }).Build();
        var error=Assert.Throws<InvalidOperationException>(()=>StartupConfiguration.Validate(config,false));
        Assert.DoesNotContain("short-do-not-print",error.Message);
        Assert.False(CorsOrigins.Matches("https://attacker.vercel.app",new[]{"https://*.vercel.app"}));
        Assert.False(CorsOrigins.Matches("http://app.test",new[]{"https://app.test"}));
        Assert.True(CorsOrigins.Matches("https://app.test",new[]{"https://app.test"}));
    }

    private static BsonDocument Render<T>(FilterDefinition<T> filter) => filter.Render(new RenderArgs<T>(BsonSerializer.SerializerRegistry.GetSerializer<T>(),BsonSerializer.SerializerRegistry));
    private static string Sign(byte[] body,string id,string timestamp,string secret) => "v1,"+Convert.ToBase64String(System.Security.Cryptography.HMACSHA256.HashData(Convert.FromBase64String(secret[6..]), Encoding.UTF8.GetBytes(id+"."+timestamp+"."+Encoding.UTF8.GetString(body))));
    private sealed class StubHandler(Func<HttpRequestMessage,HttpResponseMessage> response):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>Task.FromResult(response(request));
    }
}
