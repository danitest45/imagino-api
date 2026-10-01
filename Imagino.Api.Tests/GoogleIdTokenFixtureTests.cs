#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth;
using Imagino.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Xunit;

namespace Imagino.Api.Tests;

// The pinned Google.Apis.Auth 1.70.0 exposes no public certificate injection for
// GoogleJsonWebSignature. Isolate its test-only cached public key and restore it.
// No fixture key or validation override is included in the application project.
[CollectionDefinition("Offline Google certificates", DisableParallelization = true)]
public class OfflineGoogleCertificatesCollection { }

[Collection("Offline Google certificates")]
public class GoogleIdTokenFixtureTests
{
    [Theory]
    [InlineData("valid", true)]
    [InlineData("nonce", false)]
    [InlineData("audience", false)]
    [InlineData("expired", false)]
    [InlineData("issuer", false)]
    [InlineData("unverified", false)]
    [InlineData("signature", false)]
    [InlineData("invalid-code", false)]
    public async Task ExchangeValidatesSignedIdTokenWithoutGoogleRequests(string scenario, bool accepted)
    {
        using var trustedKey = RSA.Create(2048);
        using var wrongKey = RSA.Create(2048);
        using var certificates = new OfflineCertificateFixture(trustedKey);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var claims = new Dictionary<string, object>
        {
            ["iss"] = scenario == "issuer" ? "https://attacker.example.test" : "https://accounts.google.com",
            ["aud"] = scenario == "audience" ? "another-client" : "fixture-client",
            ["iat"] = now - 120,
            ["exp"] = scenario == "expired" ? now - 60 : now + 600,
            ["sub"] = "synthetic-google-subject",
            ["email"] = "google-fixture@example.test",
            ["email_verified"] = scenario != "unverified",
            ["nonce"] = scenario == "nonce" ? "wrong-nonce" : "fixture-nonce"
        };
        var unsigned = Base64UrlEncoder.Encode("{\"alg\":\"RS256\",\"typ\":\"JWT\"}") + "." +
            Base64UrlEncoder.Encode(JsonSerializer.Serialize(claims));
        var signingKey = scenario == "signature" ? wrongKey : trustedKey;
        var token = unsigned + "." + Base64UrlEncoder.Encode(signingKey.SignData(
            Encoding.ASCII.GetBytes(unsigned), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        var handler = new TokenHandler(scenario == "invalid-code" ? HttpStatusCode.BadRequest : HttpStatusCode.OK, token);
        using var http = new HttpClient(handler);
        var clients = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        clients.Setup(c => c.CreateClient("ProviderSecure")).Returns(http);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Google:ClientId"] = "fixture-client", ["Google:ClientSecret"] = "synthetic-client-secret",
            ["Google:RedirectUri"] = "https://api.example.test/api/auth/google/callback"
        }).Build();
        var result = await new GoogleOAuthClient(clients.Object, config).ExchangeAsync("fixture-code", "fixture-verifier", "fixture-nonce");
        if (accepted)
        {
            Assert.NotNull(result);
            Assert.Equal("synthetic-google-subject", result!.Subject);
            Assert.True(result.EmailVerified);
        }
        else Assert.Null(result);
        Assert.Equal(1, handler.Requests);
    }

    private sealed class TokenHandler(HttpStatusCode status, string token) : HttpMessageHandler
    {
        public int Requests { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests++;
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://oauth2.googleapis.com/token", request.RequestUri!.AbsoluteUri);
            var body = await request.Content!.ReadAsStringAsync(ct);
            Assert.Contains("code_verifier=fixture-verifier", body);
            Assert.Contains("redirect_uri=https%3A%2F%2Fapi.example.test%2Fapi%2Fauth%2Fgoogle%2Fcallback", body);
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(status == HttpStatusCode.OK
                    ? JsonSerializer.Serialize(new { id_token = token }) : "{\"error\":\"invalid_grant\"}")
            };
        }
    }

    private sealed class OfflineCertificateFixture : IDisposable
    {
        private readonly IDictionary cache;
        private readonly string url;
        private readonly object? previous;
        private readonly bool existed;

        public OfflineCertificateFixture(RSA publicKey)
        {
            var assembly = typeof(GoogleJsonWebSignature).Assembly;
            var verifier = assembly.GetType("Google.Apis.Auth.SignedTokenVerification", throwOnError: true)!;
            var instance = verifier.GetField("s_certificateCache", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
            var cacheBase = instance.GetType().BaseType!;
            cache = (IDictionary)cacheBase.GetField("_cache", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(instance)!;
            var constants = assembly.GetType("Google.Apis.Auth.OAuth2.GoogleAuthConsts", throwOnError: true)!;
            url = (string)constants.GetField("JsonWebKeySetUrl", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
            existed = cache.Contains(url);
            previous = cache[url];
            var entryType = cacheBase.GetNestedType("CachedCertificates", BindingFlags.NonPublic | BindingFlags.Public)!;
            cache[url] = Activator.CreateInstance(entryType, new object[] { new AsymmetricAlgorithm[] { publicKey }, DateTimeOffset.UtcNow })!;
        }

        public void Dispose()
        {
            if (existed) cache[url] = previous;
            else cache.Remove(url);
        }
    }
}
