#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Imagino.Api.Errors;
using Imagino.Api.Security;
using Imagino.Api.Services.Generation;
using Imagino.Api.Settings;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using Xunit;

namespace Imagino.Api.Tests;

public sealed class StaticMonitor<T>(T value) : IOptionsMonitor<T>
{
    public T CurrentValue => value;
    public T Get(string? name) => value;
    public IDisposable? OnChange(Action<T, string?> listener) => null;
}
public class LaunchReadinessTests
{
    [Fact]
    public async Task NewImageOutputWritesPrivateBucketAndRecoversOnlyExactOwnedKey()
    {
        var s3 = new Mock<Amazon.S3.IAmazonS3>();
        var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/xXkAAAAASUVORK5CYII=");
        var options = Options.Create(new R2StorageSettings { BucketName = "public-legacy", BucketNameVideos = "private-output" });
        var job = new GenerationJob { UserId = ObjectId.GenerateNewId().ToString(), Id = ObjectId.GenerateNewId().ToString(), Model = new() { MediaType = "image" } };
        var key = $"generation-v2/{job.UserId}/{job.Id}.png";
        s3.Setup(s => s.PutObjectAsync(It.Is<Amazon.S3.Model.PutObjectRequest>(r => r.BucketName == "private-output" && r.Key == key && r.ContentType == "image/png"), It.IsAny<CancellationToken>())).ReturnsAsync(new Amazon.S3.Model.PutObjectResponse());
        using var store = new GenerationOutputStore(options, s3.Object);
        Assert.Equal($"/api/generation/jobs/{job.Id}/media", await store.StoreAsync(job, bytes, default));
        Assert.Equal(key, job.StoredOutput!.Key);
        s3.Verify(s => s.PutObjectAsync(It.Is<Amazon.S3.Model.PutObjectRequest>(r => r.BucketName == "private-output" && r.Key == key), It.IsAny<CancellationToken>()), Times.Once);
        job.StoredOutput = null;
        s3.Setup(s => s.GetObjectAsync("private-output", key, It.IsAny<CancellationToken>())).ReturnsAsync(() => new Amazon.S3.Model.GetObjectResponse { ContentLength = bytes.Length, ResponseStream = new System.IO.MemoryStream(bytes) });
        Assert.True(await store.TryRecoverAsync(job, default));
        Assert.Equal(key, job.StoredOutput!.Key);
        Assert.Equal(bytes.Length, job.StoredOutput.Bytes);
        s3.Verify(s => s.GetObjectAsync("public-legacy", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    [Fact]
    public void RawProviderAndStorageErrorsAreNeverReturnedToUsers()
    {
        const string privateBody = "Bearer private-token data:image/png;base64,private-media ?signature=secret private-prompt";
        var mapped = Imagino.Api.Errors.ErrorMapper.Map(new Imagino.Api.Errors.UpstreamServiceException("fixture", message: privateBody));
        Assert.DoesNotContain("private-", mapped.detail);
        Assert.Null(mapped.meta);
    }
    [Fact]
    public async Task AccountDeleteCannotOrphanJobsOrLeaveLiveSessionsSilently()
    {
        using var factory = new SecurityApiFactory(); using var owner = factory.ClientFor("user-a");
        Assert.Equal(HttpStatusCode.Conflict, (await owner.DeleteAsync("/api/users/user-a")).StatusCode);
        factory.Users.Verify(s => s.DeleteAsync(It.IsAny<string>()), Times.Never);
    }
    [Fact]
    public void LaunchStagingProfileRejectsPaidFlagsAndAllowsExactBranchOnly()
    {
        var settings = new Dictionary<string, string> { ["GenerationV2:LaunchReadinessEnabled"] = "true",
            ["ASPNETCORE_ENVIRONMENT"] = "AIStaging",
            ["GenerationV2:BflHomologationEnabled"] = "true", ["GenerationV2:BflApiKey"] = "fixture-only",
            ["RENDER_SERVICE_ID"] = BflHomologationPolicy.ServiceId, ["RENDER_GIT_BRANCH"] = "feat/imagino-launch-readiness",
            ["RENDER_EXTERNAL_HOSTNAME"] = "imagino-api-ai-staging.onrender.com" };
        AIStagingConfiguration.Validate(AIStagingTests.Valid(settings));
        settings["GenerationV2:PaidGenerationEnabled"] = "true";
        Assert.Throws<InvalidOperationException>(() => AIStagingConfiguration.Validate(AIStagingTests.Valid(settings)));
    }
    internal static (GenerationModel Model, GenerationCostSettings Settings) Approved()
    {
        var model = GenerationCatalog.Seed(false).First(m => m.Provider == "bfl");
        var settings = new GenerationCostSettings { EmergencyStop = false, UserDailyCredits = 100, UserConcurrentJobs = 20, UserConcurrentVideos = 2,
            Providers = new() { ["bfl"] = new() { Enabled = true, DailyUsd = .10m, MonthlyUsd = .10m } },
            Models = new() { [model.Id] = new() { LaunchEnabled = true, PricingVersion = model.Pricing.Revision,
                ModelVersion = model.Version, Provider = model.Provider, ProviderModel = model.ProviderModel, CostBasis = "synthetic-reviewed-fixture-only",
                ReviewedAtUtc = DateTime.UtcNow.AddHours(-1), ValidUntilUtc = DateTime.UtcNow.AddHours(1),
                ExpectedRequestUsd = .01m, MaximumRequestUsd = .02m, AllowedVariance = 1, DailyUsd = .10m } } };
        return (model, settings);
    }
    [Theory]
    [InlineData("kill")][InlineData("stale")][InlineData("version")][InlineData("cost")][InlineData("missing")]
    public void CostMetadataFailsClosed(string failure)
    {
        var (model, settings) = Approved();
        var approval = settings.Models[model.Id];
        if (failure == "kill") settings.EmergencyStop = true;
        if (failure == "stale") approval.ValidUntilUtc = DateTime.UtcNow.AddSeconds(-1);
        if (failure == "version") approval.PricingVersion = "obsolete";
        if (failure == "missing") settings.Providers.Clear();
        var quote = new GenerationQuote("q", model.Id, model.Version, 1, failure == "cost" ? .03m : .01m, .01m,
            model.Pricing.Revision, DateTime.UtcNow.AddMinutes(10), new());
        Assert.Throws<ForbiddenFeatureException>(() => new GenerationCostGuard(new StaticMonitor<GenerationCostSettings>(settings)).Validate(model, quote, DateTime.UtcNow));
    }
    [Fact]
    public void QuotesAreOwnerBoundAndTamperingIsRejected()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Secret"] = new string('x', 64) }).Build();
        var signer = new GenerationQuoteAuthorization(config);
        var signed = signer.Issue("owner-a", "hash:123");
        Assert.Equal("hash:123", signer.Verify("owner-a", signed));
        Assert.Throws<ConflictAppException>(() => signer.Verify("owner-b", signed));
        Assert.Throws<ConflictAppException>(() => signer.Verify("owner-a", signed.Replace("123", "999")));
    }
    [Fact]
    public void HistoricalViewsNeverExposeProviderOrPublicStorageUrls()
    {
        var job = new GenerationJob { Id = ObjectId.GenerateNewId().ToString(), Status = GenerationStatus.Completed,
            OutputUrl = "https://public.test/private.png?token=secret", Quote = new("q", "m", "v", 3, 1, 1, "p", DateTime.UtcNow, new()) };
        Assert.Equal($"/api/generation/jobs/{job.Id}/media", GenerationJobView.From(job).OutputUrl);
    }
    [Fact]
    public async Task AcceptedReplaySurvivesCatalogChangeAndRejectsPayloadConflict()
    {
        var repo = new Mock<IGenerationRepository>();
        var request = new GenerationRequest { ModelId = "removed-model", Prompt = "private prompt" };
        var job = new GenerationJob { PayloadHash = GenerationPolicy.PayloadFingerprint(request) };
        repo.Setup(r => r.FindByKeyAsync("owner", "launch-replay-key-1234", It.IsAny<CancellationToken>())).ReturnsAsync(job);
        var service = new GenerationService(repo.Object, Array.Empty<IGenerationProvider>(), Options.Create(new GenerationSettings()));
        Assert.Same(job, await service.CreateAsync("owner", "launch-replay-key-1234", request, default));
        request.Prompt = "different";
        await Assert.ThrowsAsync<ConflictAppException>(() => service.CreateAsync("owner", "launch-replay-key-1234", request, default));
        repo.Verify(r => r.CatalogAsync(It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(r => r.ReserveAsync(It.IsAny<GenerationJob>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    [Fact]
    public async Task StoredOutputRecoveryNeverPollsOrCreatesAndUsesAcceptedCharge()
    {
        var repo = new Mock<IGenerationRepository>(); var storage = new Mock<IGenerationOutputStore>(); var provider = new Mock<IGenerationProvider>();
        var job = new GenerationJob { Status = GenerationStatus.Starting, DeadlineAt = DateTime.UtcNow.AddMinutes(-1),
            StoredOutput = new("key", "image/png", 1, "hash"), Quote = new("q", "m", "v", 7, 1, 1, "p", DateTime.UtcNow, new()) };
        var options = Options.Create(new GenerationSettings());
        var service = new GenerationService(repo.Object, new[] { provider.Object }, options);
        await new GenerationProcessor(repo.Object, new[] { provider.Object }, service, null!, storage.Object, options,
            NullLogger<GenerationProcessor>.Instance).ProcessAsync(job, default);
        storage.Verify(s => s.DownloadAsync(job, It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.SettleAsync(job, GenerationStatus.Completed, $"/api/generation/jobs/{job.Id}/media", null, It.IsAny<CancellationToken>()), Times.Once);
        provider.Verify(p => p.StartAsync(It.IsAny<GenerationJob>(), It.IsAny<CancellationToken>()), Times.Never);
        provider.Verify(p => p.PollAsync(It.IsAny<GenerationJob>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(7, job.Quote.Credits);
    }
    [Fact]
    public async Task RecoveryFindsPrivateObjectAfterPutBeforeDatabaseCrashWithoutProviderCall()
    {
        var repo = new Mock<IGenerationRepository>(); var storage = new Mock<IGenerationOutputStore>(); var provider = new Mock<IGenerationProvider>();
        var job = new GenerationJob { Status = GenerationStatus.Starting, DeadlineAt = DateTime.UtcNow.AddSeconds(-1),
            Quote = new("q", "m", "v", 7, 1, 1, "p", DateTime.UtcNow, new()) };
        storage.Setup(s => s.TryRecoverAsync(job, It.IsAny<CancellationToken>())).Callback(() => job.StoredOutput = new("owned-key", "image/png", 4, "hash")).ReturnsAsync(true);
        var options = Options.Create(new GenerationSettings()); var service = new GenerationService(repo.Object, new[] { provider.Object }, options);
        await new GenerationProcessor(repo.Object, new[] { provider.Object }, service, null!, storage.Object, options, NullLogger<GenerationProcessor>.Instance).ProcessAsync(job, default);
        repo.Verify(r => r.RecordOutputAsync(job, It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.SettleAsync(job, GenerationStatus.Completed, It.IsAny<string>(), null, It.IsAny<CancellationToken>()), Times.Once);
        provider.Verify(p => p.StartAsync(It.IsAny<GenerationJob>(), It.IsAny<CancellationToken>()), Times.Never);
        provider.Verify(p => p.PollAsync(It.IsAny<GenerationJob>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    [Theory]
    [InlineData(null)][InlineData("https://evil.test")][InlineData("null")]
    public async Task CookieRefreshRejectsUntrustedOrMissingOrigin(string? origin)
    {
        using var factory = new SecurityApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Add("Cookie", "refreshToken=private-test-cookie");
        if (origin != null) client.DefaultRequestHeaders.Add("Origin", origin);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/auth/refresh", null)).StatusCode);
        factory.RefreshTokens.Verify(r => r.ConsumeAsync(It.IsAny<string>()), Times.Never);
    }
    [Fact]
    public async Task ConcurrentLoginAbuseHasBoundedRepositoryCalls()
    {
        using var factory = new SecurityApiFactory();
        using var client = factory.CreateClient();
        var responses = await Task.WhenAll(Enumerable.Range(0, 40).Select(_ => client.PostAsync("/api/auth/login",
            new StringContent("{\"email\":\"synthetic@example.test\",\"password\":\"fixture\"}", System.Text.Encoding.UTF8, "application/json"))));
        Assert.Equal(10, responses.Count(r => r.StatusCode == HttpStatusCode.Unauthorized));
        Assert.Equal(30, responses.Count(r => r.StatusCode == HttpStatusCode.TooManyRequests));
        factory.UserRepository.Verify(r => r.GetByEmailAsync("synthetic@example.test"), Times.Exactly(10));
    }
    [Theory]
    [InlineData("media")][InlineData("download")]
    public async Task MediaContractEnforcesOwnershipAnonymousAndRange(string endpoint)
    {
        using var factory = new SecurityApiFactory();
        factory.ExtraSettings["GenerationV2:Enabled"] = "true";
        factory.ExtraSettings["ImageGeneratorSettings:MongoDatabase"] = "imagino_staging";
        factory.ExtraSettings["R2Settings:BucketName"] = "imagino-images-staging";
        factory.ExtraSettings["R2Settings:BucketNameVideos"] = "imagino-videos-staging";
        var job = new GenerationJob { Id = ObjectId.GenerateNewId().ToString(), UserId = "user-a", Status = GenerationStatus.Completed,
            Model = new() { MediaType = "image" }, Quote = new("q", "m", "v", 1, 0, 0, "p", DateTime.UtcNow, new()) };
        factory.GenerationJobs.Setup(r => r.GetAsync(job.Id, "user-a", It.IsAny<CancellationToken>())).ReturnsAsync(job);
        factory.GenerationStorage.Setup(s => s.DownloadAsync(job, It.IsAny<CancellationToken>())).ReturnsAsync((new byte[] { 1, 2, 3, 4 }, "image/png"));
        using var anonymous = factory.CreateClient(); using var foreign = factory.ClientFor("user-b"); using var owner = factory.ClientFor("user-a");
        var path = $"/api/generation/jobs/{job.Id}/{endpoint}";
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await foreign.GetAsync(path)).StatusCode);
        owner.DefaultRequestHeaders.Range = new System.Net.Http.Headers.RangeHeaderValue(1, 2);
        var response = await owner.GetAsync(path);
        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
        Assert.Equal(new byte[] { 2, 3 }, await response.Content.ReadAsByteArrayAsync());
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        if (endpoint == "download") Assert.Contains(job.Id, response.Content.Headers.ContentDisposition!.FileName!);
        factory.GenerationStorage.Verify(s => s.DownloadAsync(job, It.IsAny<CancellationToken>()), Times.Once);
    }
}

public sealed class LocalMongoFactAttribute : FactAttribute
{
    public LocalMongoFactAttribute() { if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IMAGINO_TEST_MONGO_URI"))) Skip = "Isolated local Mongo replica set required."; }
}
public class LaunchMongoIntegrationTests
{
    [LocalMongoFact]
    public async Task SyntheticBackupRestoresPricingWalletJobsAndBudgetExactlyToNewDatabase()
    {
        var uri = Environment.GetEnvironmentVariable("IMAGINO_TEST_MONGO_URI")!;
        Assert.StartsWith("mongodb://127.0.0.1:", uri);
        var client = new MongoClient(uri); var source = "launch_tests_backup_" + Guid.NewGuid().ToString("N");
        var target = "launch_tests_restore_" + Guid.NewGuid().ToString("N");
        var collections = new[] { "Users", "generation_models_v2", "generation_jobs_v2", "generation_ledger_v2", "generation_budget_reservations_v1" };
        try {
            foreach (var collection in collections) {
                var synthetic = new BsonDocument { { "_id", "synthetic" }, { "fixture", true }, { "version", "accepted-v1" }, { "amount", 7 } };
                await client.GetDatabase(source).GetCollection<BsonDocument>(collection).InsertOneAsync(synthetic);
                var export = await client.GetDatabase(source).GetCollection<BsonDocument>(collection).Find(_ => true).ToListAsync();
                await client.GetDatabase(target).GetCollection<BsonDocument>(collection).InsertManyAsync(export);
                var restored = await client.GetDatabase(target).GetCollection<BsonDocument>(collection).Find(_ => true).ToListAsync();
                Assert.Equal(export.Select(d => d.ToJson()), restored.Select(d => d.ToJson()));
            }
        } finally {
            Assert.StartsWith("launch_tests_", source); Assert.StartsWith("launch_tests_", target);
            await client.DropDatabaseAsync(source); await client.DropDatabaseAsync(target);
        }
    }
    [LocalMongoFact]
    public async Task TenWorkersCannotExceedBudgetAndUnknownAttemptRemainsEncumbered()
    {
        var uri = Environment.GetEnvironmentVariable("IMAGINO_TEST_MONGO_URI")!;
        Assert.StartsWith("mongodb://127.0.0.1:", uri);
        var client = new MongoClient(uri);
        var name = "launch_tests_" + Guid.NewGuid().ToString("N");
        var db = client.GetDatabase(name);
        try {
            var (model, settings) = LaunchReadinessTests.Approved();
            var repo = new MongoGenerationRepository(client, Options.Create(new ImageGeneratorSettings { MongoDatabase = name }),
                Options.Create(new GenerationSettings()), new GenerationCostGuard(new StaticMonitor<GenerationCostSettings>(settings)));
            await repo.InitializeAsync(new[] { model }, default);
            var owner = ObjectId.GenerateNewId().ToString();
            await db.GetCollection<Imagino.Api.Models.User>("Users").InsertOneAsync(new() { Id = owner, Credits = 100 });
            var tasks = Enumerable.Range(0, 10).Select(async i => {
                var job = new GenerationJob { UserId = owner, IdempotencyKey = "budget-fixture-" + i, RequestHash = "hash-" + i, Model = model,
                    DeadlineAt = DateTime.UtcNow.AddMinutes(2), Quote = new("q", model.Id, model.Version, 1, .01m, .01m, model.Pricing.Revision, DateTime.UtcNow.AddMinutes(10), new()) };
                try { return await repo.ReserveAsync(job, default); }
                catch (ForbiddenFeatureException) { return null; }
                catch (MongoWriteException e) when (e.WriteError.Category == ServerErrorCategory.DuplicateKey) { return null; }
                catch (MongoCommandException e) when (e.Code == 11000) { return null; }
            });
            var accepted = (await Task.WhenAll(tasks)).Where(j => j != null).ToArray();
            Assert.Equal(5, accepted.Length);
            var counters = await db.GetCollection<GenerationBudgetCounter>("generation_budget_counters_v1").Find(_ => true).ToListAsync();
            Assert.All(counters.Where(c => c.Id.StartsWith("provider/") || c.Id.StartsWith("model/")), c => Assert.Equal(accepted.Length * .02m, c.Used));
            Assert.Equal(100 - accepted.Length, (await db.GetCollection<Imagino.Api.Models.User>("Users").Find(u => u.Id == owner).FirstAsync()).Credits);
            var claimed = await repo.ClaimAsync(default); Assert.NotNull(claimed);
            Assert.True(await repo.BeginCostSubmissionAsync(claimed!, default));
            Assert.False(await repo.BeginCostSubmissionAsync(claimed!, default));
            Assert.True(await repo.SettleAsync(claimed!, GenerationStatus.Failed, null, "submission_unknown", default));
            Assert.False(await repo.SettleAsync(claimed!, GenerationStatus.Failed, null, "submission_unknown", default));
            var reservation = await db.GetCollection<GenerationBudgetReservation>("generation_budget_reservations_v1").Find(r => r.Id == claimed!.Id).FirstAsync();
            Assert.True(reservation.Attempted); Assert.True(reservation.Closed);
            var after = await db.GetCollection<GenerationBudgetCounter>("generation_budget_counters_v1").Find(c => c.Id.StartsWith("provider/")).ToListAsync();
            Assert.All(after, c => Assert.Equal(accepted.Length * .02m, c.Used));
            var limiter = new DurableRequestLimiter(client, Options.Create(new ImageGeneratorSettings { MongoDatabase = name }));
            var allowed = await Task.WhenAll(Enumerable.Range(0, 40).Select(_ => limiter.AllowAsync("concurrent-fixture", new(10, 60), default)));
            Assert.Equal(10, allowed.Count(a => a));
        } finally {
            Assert.StartsWith("launch_tests_", name);
            await client.DropDatabaseAsync(name);
        }
    }
}
