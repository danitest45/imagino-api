#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Imagino.Api.DTOs;
using Imagino.Api.Models;
using Imagino.Api.Repository;
using Imagino.Api.Security;
using Imagino.Api.Services.Storage;
using Imagino.Api.Services.WebhookImage;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace Imagino.Api.Tests;

public class GeneratedImageSecurityTests
{
    [Fact]
    public void ValidGeneratedPngCanExceedAvatarLimit()
    {
        var bytes = CreatePng();
        Assert.InRange(bytes.Length, AvatarValidator.MaxBytes + 1, GeneratedImageValidator.MaxBytes - 1);
        Assert.Equal((".png", "image/png"), GeneratedImageValidator.Identify(bytes));
        Assert.Throws<ArgumentException>(() => AvatarValidator.Identify(bytes));
    }

    [Fact]
    public void GeneratedImageOver20MiBIsRejectedEvenWithValidSignature()
    {
        var bytes = new byte[GeneratedImageValidator.MaxBytes + 1];
        new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(bytes, 0);
        Assert.Throws<ArgumentException>(() => GeneratedImageValidator.Identify(bytes));
    }

    [Theory]
    [InlineData("<svg></svg>")]
    [InlineData("<html>bad</html>")]
    [InlineData("MZ executable")]
    [InlineData("GIF89a")]
    public void UnsupportedGeneratedFormatIsRejected(string data) =>
        Assert.Throws<ArgumentException>(() => GeneratedImageValidator.Identify(Encoding.UTF8.GetBytes(data)));

    [Fact]
    public void GeneratedJpegAndWebpUseMagicBytes()
    {
        Assert.Equal((".jpg", "image/jpeg"), GeneratedImageValidator.Identify(new byte[] { 255, 216, 255, 1 }));
        Assert.Equal((".webp", "image/webp"), GeneratedImageValidator.Identify("RIFFxxxxWEBP"u8));
        Assert.Throws<ArgumentException>(() => GeneratedImageValidator.Identify(Array.Empty<byte>()));
        Assert.Throws<ArgumentException>(() => GeneratedImageValidator.Identify(new byte[] { 0, 0, 0, 24, 102, 116, 121, 112, 97, 118, 105, 102 }));
    }

    [Theory]
    [InlineData("Replicate")]
    [InlineData("RunPod")]
    public async Task WebhookStoresGeneratedImageOver5MiB(string provider)
    {
        var bytes = CreatePng();
        var (service, jobs, storage) = CreateService(provider, bytes);
        storage.Setup(s => s.UploadAsync(It.IsAny<Stream>(), "images/local-job.png", "image/png", It.IsAny<CancellationToken>()))
            .Callback<Stream, string, string, CancellationToken>((stream, _, _, _) => Assert.Equal(bytes.Length, stream.Length))
            .ReturnsAsync("https://storage.test/generated.png");
        var result = await Process(service, provider, bytes);
        Assert.Equal("Completed", result.Status);
        storage.Verify(s => s.UploadAsync(It.IsAny<Stream>(), "images/local-job.png", "image/png", It.IsAny<CancellationToken>()), Times.Once);
        jobs.Verify(r => r.CompleteWebhookAsync("local-job", It.IsAny<string>(), "https://storage.test/generated.png"), Times.Once);
    }

    [Theory]
    [InlineData("Replicate", false)]
    [InlineData("Replicate", true)]
    [InlineData("RunPod", false)]
    [InlineData("RunPod", true)]
    public async Task InvalidOrOversizedWebhookOutputCannotReachStorage(string provider, bool oversized)
    {
        var bytes = oversized ? new byte[GeneratedImageValidator.MaxBytes + 1] : Encoding.UTF8.GetBytes("<svg></svg>");
        if (oversized) new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(bytes, 0);
        var (service, jobs, storage) = CreateService(provider, bytes);
        await Assert.ThrowsAnyAsync<ArgumentException>(() => Process(service, provider, bytes));
        storage.Verify(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        jobs.Verify(r => r.CompleteWebhookAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        jobs.Verify(r => r.ReleaseWebhookAsync("local-job", It.IsAny<string>()), Times.Once);
    }

    [Theory]
    [InlineData("ReplicateSettings:ApiKey")]
    [InlineData("ReplicateSettings:WebhookUrl")]
    public void ConfiguredReplicateRequiresSigningSecret(string enabledSetting)
    {
        var settings = ValidCoreSettings();
        settings[enabledSetting] = "private-value-do-not-log";
        settings["Webhooks:ReplicateSigningSecret"] = " ";
        var error = Assert.Throws<InvalidOperationException>(() => StartupConfiguration.Validate(Config(settings), false));
        Assert.Contains("Webhooks:ReplicateSigningSecret", error.Message);
        Assert.DoesNotContain("private-value-do-not-log", error.Message);
        settings["Webhooks:ReplicateSigningSecret"] = "test-signing-secret";
        StartupConfiguration.Validate(Config(settings), false);
    }

    [Fact]
    public void DisabledReplicateDoesNotRequireSigningSecret() => StartupConfiguration.Validate(Config(ValidCoreSettings()), false);

    [Fact]
    public async Task SignedRunPodEnvelopeCanCarryGeneratedImageBeyondOld16MiBBodyLimit()
    {
        var bytes = CreatePng(7000);
        var payload = new RunPodContentResponse { id = "prediction", status = "COMPLETED", output = new Output { images = new() { Convert.ToBase64String(bytes) } } };
        var body = JsonSerializer.SerializeToUtf8Bytes(payload);
        Assert.InRange(body.Length, 16 * 1024 * 1024 + 1, GeneratedImageValidator.MaxWebhookBodyBytes);
        Assert.Equal((".png", "image/png"), GeneratedImageValidator.Identify(bytes));
        const string secret = "whsec_dGVzdC1zaWduaW5nLXNlY3JldA==";
        using var factory = new SecurityApiFactory();
        factory.ExtraSettings["Webhooks:RunPodEnabled"] = "true";
        factory.ExtraSettings["Webhooks:RunPodSigningSecret"] = secret;
        factory.Webhooks.Setup(s => s.ProcessarWebhookRunPodAsync(It.IsAny<RunPodContentResponse>()))
            .ReturnsAsync(new JobStatusResponse { Status = "Completed" });
        using var client = factory.CreateClient();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var prefix = Encoding.UTF8.GetBytes("message." + timestamp + ".");
        var signed = new byte[prefix.Length + body.Length];
        prefix.CopyTo(signed, 0); body.CopyTo(signed, prefix.Length);
        var signature = "v1," + Convert.ToBase64String(System.Security.Cryptography.HMACSHA256.HashData(Convert.FromBase64String(secret[6..]), signed));
        client.DefaultRequestHeaders.Add("webhook-id", "message");
        client.DefaultRequestHeaders.Add("webhook-timestamp", timestamp);
        client.DefaultRequestHeaders.Add("webhook-signature", signature);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/webhooks/runpod", new ByteArrayContent(body))).StatusCode);
        factory.Webhooks.Verify(s => s.ProcessarWebhookRunPodAsync(It.Is<RunPodContentResponse>(p => p.id == "prediction")), Times.Once);
    }

    private static Task<JobStatusResponse> Process(WebhookImageService service, string provider, byte[] bytes) => provider == "Replicate"
        ? service.ProcessarWebhookReplicateAsync(new ReplicateWebhookRequest { Id = "prediction", Status = "succeeded", Output = "https://replicate.delivery/output.png" })
        : service.ProcessarWebhookRunPodAsync(new RunPodContentResponse { id = "prediction", status = "COMPLETED", output = new Output { images = new() { "data:image/png;base64," + Convert.ToBase64String(bytes) } } });

    private static (WebhookImageService Service, Mock<IImageJobRepository> Jobs, Mock<IStorageService> Storage) CreateService(string provider, byte[] bytes)
    {
        var job = new ImageJob { Id = "local-job", JobId = "job", ProviderJobId = "prediction", UserId = "owner", CallbackProvider = provider, TokenConsumed = true, Status = ImageJobStatus.Running };
        var jobs = new Mock<IImageJobRepository>();
        jobs.Setup(r => r.GetByProviderJobIdAsync("prediction")).ReturnsAsync(job);
        jobs.Setup(r => r.TryClaimWebhookAsync(job, It.IsAny<string>())).ReturnsAsync(true);
        jobs.Setup(r => r.CompleteWebhookAsync(job.Id, It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        var users = new Mock<IUserRepository>();
        users.Setup(r => r.GetByIdAsync("owner")).ReturnsAsync(new User { Id = "owner" });
        var storage = new Mock<IStorageService>();
        var clients = new Mock<IHttpClientFactory>();
        clients.Setup(c => c.CreateClient("ProviderSecure")).Returns(new HttpClient(new ImageHandler(bytes)));
        return (new WebhookImageService(jobs.Object, users.Object, storage.Object, new SafeMediaDownloader(clients.Object)), jobs, storage);
    }

    private sealed class ImageHandler(byte[] bytes) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
    }

    private static IConfiguration Config(Dictionary<string, string?> settings) => new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    private static Dictionary<string, string?> ValidCoreSettings() => new()
    {
        ["ImageGeneratorSettings:MongoConnection"] = "mongodb://127.0.0.1:27017",
        ["ImageGeneratorSettings:MongoDatabase"] = "test",
        ["Jwt:Secret"] = "local-test-secret-with-at-least-32-bytes",
        ["Jwt:Issuer"] = "test", ["Jwt:Audience"] = "test",
        ["Frontend:BaseUrl"] = "https://app.test",
        ["RefreshTokenCookie:HttpOnly"] = "true", ["RefreshTokenCookie:Secure"] = "true",
        ["RefreshTokenCookie:SameSite"] = "None", ["RefreshTokenCookie:ExpiresDays"] = "7"
    };

    // A complete PNG (IHDR, uncompressed zlib scanlines, CRCs and IEND), not just a magic-byte stub.
    private static byte[] CreatePng(int height = 3072)
    {
        const int width = 2048;
        using var png = new MemoryStream();
        png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var header = new byte[13];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header, width);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8; // 8-bit grayscale, filter/compression/interlace methods zero.
        Chunk(png, "IHDR", header);
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.NoCompression, leaveOpen: true))
        {
            var row = new byte[width + 1]; // Filter byte zero and grayscale pixels.
            for (var y = 0; y < height; y++) zlib.Write(row);
        }
        Chunk(png, "IDAT", compressed.ToArray());
        Chunk(png, "IEND", Array.Empty<byte>());
        return png.ToArray();
    }

    private static void Chunk(Stream output, string type, byte[] bytes)
    {
        Span<byte> number = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(number, bytes.Length);
        output.Write(number);
        var kind = Encoding.ASCII.GetBytes(type);
        output.Write(kind); output.Write(bytes);
        uint crc = uint.MaxValue;
        foreach (var part in new[] { kind, bytes }) foreach (var value in part)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0);
        }
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(number, ~crc);
        output.Write(number);
    }
}
