using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Imagino.Api.Errors;
using Imagino.Api.Security;
using Imagino.Api.Services.Generation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Moq;
using Xunit;

namespace Imagino.Api.Tests;

public class BflHomologationTests
{
    private static readonly DateTime AuthorizationTime = new(2026, 10, 5, 20, 0, 0, DateTimeKind.Utc);
    private sealed class AuthorizationClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(AuthorizationTime);
    }
    private static byte[] Reference() => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "bfl-reference-20261005.png"));
    private static GenerationJob Job(int call)
    {
        var model = GenerationCatalog.Seed(true).Single(m => m.Id == (call == 1 ? "flux-fast-20261002" : "flux-studio-20261002"));
        var request = new GenerationRequest { ModelId = model.Id, Prompt = call switch {
            1 => BflHomologationPolicy.FastPrompt, 2 => BflHomologationPolicy.StudioPrompt, _ => BflHomologationPolicy.ReferencePrompt } };
        if (call == 3) request.Inputs.Add(new("reference", "data:image/png;base64," + Convert.ToBase64String(Reference())));
        var input = GenerationPolicy.Validate(model, request);
        return new GenerationJob { Model = model, UserId = BflHomologationPolicy.OwnerId, Prompt = input.Prompt,
            Settings = input.Settings, Inputs = input.Inputs, Quote = GenerationPolicy.Quote(model, input, DateTime.UtcNow),
            IdempotencyKey = $"bfl-homologation-20261005-call-{call}", RequestHash = GenerationPolicy.Fingerprint(model, input),
            Status = GenerationStatus.Starting, DeadlineAt = DateTime.UtcNow.AddMinutes(10), Lease = "lease" };
    }
    private static IOptions<GenerationSettings> OptionsForTest() => Options.Create(new GenerationSettings {
        Enabled = true, StagingFixtureEnabled = true, BflHomologationEnabled = true, PaidGenerationEnabled = true, BflApiKey = "unit-only" });
    private static Dictionary<string,string> ScopedConfiguration() => new() {
        ["ASPNETCORE_ENVIRONMENT"] = "AIStaging", ["RENDER_SERVICE_ID"] = BflHomologationPolicy.ServiceId,
        ["RENDER_GIT_BRANCH"] = "codex/imagino-ai-revival-v2", ["RENDER_EXTERNAL_HOSTNAME"] = "imagino-api-ai-staging.onrender.com",
        ["GenerationV2:BflHomologationEnabled"] = "true", ["GenerationV2:BflApiKey"] = "unit-only" };

    [Theory]
    [InlineData(1,5,0.014)] [InlineData(2,10,0.030)] [InlineData(3,15,0.045)]
    public void OnlyTheThreeApprovedRequestsMatchTheirExactCosts(int call, int credits, double cost)
    {
        var job = Job(call);
        Assert.Equal(call, BflHomologationPolicy.ValidateJob(job, AuthorizationTime));
        Assert.Equal(credits, job.Quote.Credits); Assert.Equal((decimal)cost, job.Quote.ProviderCostEstimateUsd);
        Assert.Equal((1024,1024), GenerationPolicy.Dimensions(job.Settings));
    }
    [Fact]
    public void AuthorizationRejectsOtherOwnerResolutionPromptKeyOrProviderBeforeReservation()
    {
        foreach (var alter in new Action<GenerationJob>[] {
            j => j.UserId = "foreign", j => j.Settings["resolution"] = "4MP", j => j.Settings["aspectRatio"] = "16:9",
            j => j.Prompt += " another output", j => j.IdempotencyKey = "a-different-request-key",
            j => j.Model.ProviderModel = "flux-3-image", j => j.Model.Version = "changed",
            j => j.Quote = j.Quote with { ProviderCostEstimateUsd = 0.16m }, j => j.RequestHash = "tampered" })
        {
            var job = Job(1); alter(job);
            Assert.Throws<ForbiddenFeatureException>(() => BflHomologationPolicy.ValidateJob(job, AuthorizationTime));
        }
    }
    [Fact]
    public void OnlyTheControlledReferenceAndExactlyOneInputAreAllowed()
    {
        var job = Job(3); job.Inputs.Add(job.Inputs[0]);
        Assert.Throws<ForbiddenFeatureException>(() => BflHomologationPolicy.ValidateJob(job, AuthorizationTime));
        job = Job(3); var bytes = Reference(); bytes[^1] ^= 1;
        job.Inputs[0] = new("reference", "data:image/png;base64," + Convert.ToBase64String(bytes));
        Assert.Throws<ForbiddenFeatureException>(() => BflHomologationPolicy.ValidateJob(job, AuthorizationTime));
    }
    [Fact]
    public void AuthorizationExpiresAndOtherProvidersRemainUnavailable()
    {
        var job = Job(1);
        Assert.Throws<ForbiddenFeatureException>(() => BflHomologationPolicy.Validate(job.UserId, job.Model,
            new(job.Prompt, job.Settings, job.Inputs), job.Quote, BflHomologationPolicy.ExpiresAtUtc));
        var service = new GenerationService(Mock.Of<IGenerationRepository>(), Array.Empty<IGenerationProvider>(), OptionsForTest());
        foreach (var model in GenerationCatalog.Seed(true).Where(m => m.Provider is not ("bfl" or "fixture")))
            Assert.NotEqual("ready", service.Availability(model));
        Assert.Equal("approval_required", service.Availability(job.Model, BflHomologationPolicy.ExpiresAtUtc));
    }
    [Fact]
    public void ScopedProfileAllowsKeyWithoutEnablingSpendingAndRegistersNoOtherPaidProviders()
    {
        var config = AIStagingTests.Valid(ScopedConfiguration()); AIStagingConfiguration.Validate(config);
        var services = new ServiceCollection(); services.AddGenerationV2(config, fixtureOnly:true);
        Assert.Equal(new[] { typeof(BflGenerationProvider), typeof(StagingGenerationProvider) }, services
            .Where(s=>s.ServiceType==typeof(IGenerationProvider)).Select(s=>s.ImplementationType));
        var options = OptionsForTest(); options.Value.PaidGenerationEnabled = false;
        var provider = new Mock<IGenerationProvider>(); provider.SetupGet(p=>p.Name).Returns("bfl"); provider.SetupGet(p=>p.IsConfigured).Returns(true);
        Assert.Equal("approval_required", new GenerationService(Mock.Of<IGenerationRepository>(), new[]{provider.Object},options).Availability(Job(1).Model));
    }
    [Theory]
    [InlineData("RENDER_SERVICE_ID", "srv-dauhp20u01pc73fa8ctg")]
    [InlineData("RENDER_GIT_BRANCH", "master")]
    [InlineData("RENDER_EXTERNAL_HOSTNAME", "production.example")]
    [InlineData("ASPNETCORE_ENVIRONMENT", "Staging")]
    [InlineData("GenerationV2:GeminiApiKey", "unit-only")]
    public void ScopedProfileRejectsOtherServicesBranchesProfilesAndKeys(string key,string value)
    {
        var data=ScopedConfiguration();data[key]=value;
        Assert.Throws<InvalidOperationException>(()=>AIStagingConfiguration.Validate(AIStagingTests.Valid(data)));
    }
    [Fact]
    public async Task ForeignOwnerCannotObtainQuoteOrReachWalletReservation()
    {
        var job=Job(1);var repo=new Mock<IGenerationRepository>();
        repo.Setup(r=>r.CatalogAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<GenerationModel>{job.Model});
        var provider=new Mock<IGenerationProvider>();provider.SetupGet(p=>p.Name).Returns("bfl");provider.SetupGet(p=>p.IsConfigured).Returns(true);
        var service=new GenerationService(repo.Object,new[]{provider.Object},OptionsForTest());
        var request=new GenerationRequest{ModelId=job.Model.Id,Prompt=job.Prompt,QuoteId=job.Quote.QuoteId};
        await Assert.ThrowsAsync<ForbiddenFeatureException>(()=>service.QuoteAsync(request,default,"foreign"));
        await Assert.ThrowsAsync<ForbiddenFeatureException>(()=>service.CreateAsync("foreign",job.IdempotencyKey,request,default));
        repo.Verify(r=>r.ReserveAsync(It.IsAny<GenerationJob>(),It.IsAny<CancellationToken>()),Times.Never);
    }
    [Fact]
    public async Task DurableSubmissionDenialPreventsPostAndAnAmbiguousPostIsNeverRepeated()
    {
        var job=Job(1);var repo=new Mock<IGenerationRepository>();
        repo.Setup(r=>r.CatalogAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<GenerationModel>{job.Model});
        repo.SetupSequence(r=>r.BeginBflSubmissionAsync(job,It.IsAny<CancellationToken>())).ReturnsAsync(true).ReturnsAsync(false);
        var provider=new Mock<IGenerationProvider>();provider.SetupGet(p=>p.Name).Returns("bfl");provider.SetupGet(p=>p.IsConfigured).Returns(true);
        provider.Setup(p=>p.StartAsync(job,It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException());
        var opts=OptionsForTest(); var service=new GenerationService(repo.Object,new[]{provider.Object},opts, clock: new AuthorizationClock());
        var processor=new GenerationProcessor(repo.Object,new[]{provider.Object},service,new(Mock.Of<IHttpClientFactory>()),Mock.Of<IGenerationOutputStore>(),opts,NullLogger<GenerationProcessor>.Instance);
        await processor.ProcessAsync(job,default); await processor.ProcessAsync(job,default);
        provider.Verify(p=>p.StartAsync(job,It.IsAny<CancellationToken>()),Times.Once);
        repo.Verify(r=>r.SettleAsync(job,GenerationStatus.Failed,null,"submission_unknown",It.IsAny<CancellationToken>()),Times.Once);
        repo.Verify(r=>r.SettleAsync(job,GenerationStatus.Failed,null,"bfl_submission_blocked",It.IsAny<CancellationToken>()),Times.Once);
    }
    [Fact]
    public void FiniteLedgerPersistsExactDecimalsAndHasOnlyThreeSlots()
    {
        var ledger=new BflHomologationLedger();var bson=ledger.ToBsonDocument();
        Assert.Equal(BsonType.Decimal128,bson["BudgetUsd"].BsonType);
        var copy=BsonSerializer.Deserialize<BflHomologationLedger>(bson);
        Assert.Equal(0.15m,copy.BudgetUsd);Assert.Equal(3,copy.Calls.Count);
        Assert.Equal(0.089m,copy.Calls.Values.Sum(c=>c.EstimatedUsd));
        copy.Calls["1"].JobId="durable-job";copy.Calls["1"].State="SubmissionAttempted";copy.Halted=true;
        copy=BsonSerializer.Deserialize<BflHomologationLedger>(copy.ToBson());
        Assert.Equal("SubmissionAttempted",copy.Calls["1"].State);Assert.True(copy.Halted);
    }
    [Fact]
    public void OutputValidationRejectsWrongDimensionsAndFormat()
    {
        var png=Reference();Assert.Equal((1024,1024),BflHomologationPolicy.ValidateOutput(png));
        png[19]=1;Assert.Throws<InvalidDataException>(()=>BflHomologationPolicy.ValidateOutput(png));
        Assert.Throws<InvalidDataException>(()=>BflHomologationPolicy.ValidateOutput(new byte[40]));
    }
}
