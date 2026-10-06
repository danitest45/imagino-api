using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Imagino.Api.Controllers;
using Imagino.Api.DependencyInjection;
using Imagino.Api.Security;
using Imagino.Api.Services.Generation;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Imagino.Api.Tests;

public class AIStagingTests
{
    internal static IConfiguration Valid(Dictionary<string, string> extra = null)
    {
        var data = new Dictionary<string, string> {
            ["ImageGeneratorSettings:MongoConnection"] = "mongodb://localhost", ["ImageGeneratorSettings:MongoDatabase"] = "imagino_staging",
            ["Jwt:Secret"] = new string('x', 32), ["Jwt:Issuer"] = "staging", ["Jwt:Audience"] = "staging",
            ["Frontend:BaseUrl"] = AIStagingConfiguration.Preview, ["Cors:AllowedOrigins:0"] = AIStagingConfiguration.Preview,
            ["Cors:AllowedOrigins:1"] = AIStagingConfiguration.WorkingStudioPreview,
            ["R2Settings:BucketName"] = "imagino-images-staging", ["R2Settings:BucketNameVideos"] = "imagino-videos-staging",
            ["R2Settings:AccessKeyId"] = "test", ["R2Settings:SecretAccessKey"] = "test",
            ["R2Settings:ServiceUrl"] = "https://f3915d7185410d3a7d3a9599e22194af.r2.cloudflarestorage.com",
            ["R2Settings:PublicUrl"] = "https://pub-56f86851d1884a3b8e7a73f1624e4239.r2.dev",
            ["RefreshTokenCookie:Secure"] = "true", ["RefreshTokenCookie:HttpOnly"] = "true", ["RefreshTokenCookie:SameSite"] = "None",
            ["GenerationV2:Enabled"] = "true", ["GenerationV2:SeedStagingCatalog"] = "true", ["GenerationV2:StagingFixtureEnabled"] = "true"
        };
        if (extra != null) foreach (var entry in extra) data[entry.Key] = entry.Value;
        return new ConfigurationBuilder().AddInMemoryCollection(data).Build();
    }
    [Theory]
    [InlineData("Stripe:ApiKey", "never-copy")]
    [InlineData("GenerationV2:PaidGenerationEnabled", "true")]
    [InlineData("GenerationV2:BflApiKey", "never-copy")]
    [InlineData("GenerationV2:GeminiApiKey", "never-copy")]
    [InlineData("GenerationV2:StagingFixtureDelaySeconds", "121")]
    [InlineData("ImageGeneratorSettings:MongoDatabase", "imagino")]
    [InlineData("Cors:AllowedOrigins:1", "https://other.example")]
    [InlineData("RefreshTokenCookie:Domain", "imagino-api-staging.onrender.com")]
    public void DedicatedProfileRejectsForbiddenConfiguration(string key, string value) =>
        Assert.Throws<InvalidOperationException>(() => AIStagingConfiguration.Validate(Valid(new() { [key] = value })));

    [Fact]
    public void DedicatedProfileAcceptsOnlyBothKnownOriginsInEitherOrder()
    {
        AIStagingConfiguration.Validate(Valid());
        AIStagingConfiguration.Validate(Valid(new() {
            ["Cors:AllowedOrigins:0"] = AIStagingConfiguration.WorkingStudioPreview,
            ["Cors:AllowedOrigins:1"] = AIStagingConfiguration.Preview
        }));
    }

    [Theory]
    [InlineData("Cors:AllowedOrigins:1", "")]
    [InlineData("Cors:AllowedOrigins:1", AIStagingConfiguration.Preview)]
    [InlineData("Cors:AllowedOrigins:1", "https://*.vercel.app")]
    [InlineData("Cors:AllowedOrigins:1", "*")]
    [InlineData("Cors:AllowedOrigins:1", "https://external.example")]
    [InlineData("Cors:AllowedOrigins:1", AIStagingConfiguration.WorkingStudioPreview + "/")]
    [InlineData("Cors:AllowedOrigins:2", "https://external.example")]
    [InlineData("Cors:AllowedOrigins:2", AIStagingConfiguration.WorkingStudioPreview)]
    [InlineData("Frontend:BaseUrl", AIStagingConfiguration.WorkingStudioPreview)]
    public void DedicatedProfileRejectsMissingDuplicateOrAdditionalOriginsAndChangedBaseUrl(string key, string value) =>
        Assert.Throws<InvalidOperationException>(() => AIStagingConfiguration.Validate(Valid(new() { [key] = value })));

    [Fact]
    public void DedicatedProfileRegistersOnlySyntheticGenerationAndNoBilling()
    {
        var config = Valid(); AIStagingConfiguration.Validate(config);
        var services = new ServiceCollection(); services.AddAIStagingServices(config); services.AddGenerationV2(config, fixtureOnly: true);
        Assert.DoesNotContain(services, s => s.ServiceType.Name.Contains("Billing") || s.ServiceType.Name.Contains("Stripe"));
        Assert.Single(services.Where(s => s.ServiceType == typeof(IGenerationProvider)));
        Assert.Equal(typeof(StagingGenerationProvider), services.Single(s => s.ServiceType == typeof(IGenerationProvider)).ImplementationType);
    }
    [Fact]
    public async System.Threading.Tasks.Task DelayedFixtureHonorsShutdownBeforeProducingOutput()
    {
        var provider = new StagingGenerationProvider(Microsoft.Extensions.Options.Options.Create(new GenerationSettings {
            StagingFixtureEnabled = true, StagingFixtureDelaySeconds = 60
        }));
        var job = new GenerationJob { Id = "synthetic-test", ProviderJobId = "fixture-synthetic-test" };
        using var stopping = new System.Threading.CancellationTokenSource(); stopping.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.PollAsync(job, stopping.Token));
    }
    [Fact]
    public void DedicatedProfileDoesNotExposeBillingLegacyGenerationOrUserMutation()
    {
        var application = new ApplicationModel();
        foreach (var type in typeof(GenerationController).Assembly.GetTypes().Where(t => t.Namespace == "Imagino.Api.Controllers" && t.Name.EndsWith("Controller")))
        {
            var controller = new ControllerModel(type.GetTypeInfo(), type.GetCustomAttributes().ToArray());
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                controller.Actions.Add(new ActionModel(method, method.GetCustomAttributes().ToArray()) { ActionName = method.Name });
            application.Controllers.Add(controller);
        }
        new AIStagingControllerConvention().Apply(application);
        Assert.Equal(new[] { "AuthController", "GenerationController", "HealthController", "UsersController" }, application.Controllers.Select(c => c.ControllerType.Name).OrderBy(n => n));
        Assert.Equal(new[] { "Login", "Logout", "Refresh" }, application.Controllers.Single(c => c.ControllerType.Name == "AuthController").Actions.Select(a => a.ActionName).OrderBy(n => n));
        Assert.Equal(new[] { "GetCredits", "GetMe" }, application.Controllers.Single(c => c.ControllerType.Name == "UsersController").Actions.Select(a => a.ActionName).OrderBy(n => n));
    }
}
