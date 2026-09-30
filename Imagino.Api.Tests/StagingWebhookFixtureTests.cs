#nullable enable
using System;
using System.Collections.Generic;
using Imagino.Api.Security;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Imagino.Api.Tests;

public class StagingWebhookFixtureTests
{
    private const string Host = "pub-56f86851d1884a3b8e7a73f1624e4239.r2.dev";
    private static Dictionary<string, string?> Settings() => new()
    {
        ["Webhooks:StagingFixturesEnabled"] = "true", ["Webhooks:StagingFixtureHost"] = Host,
        ["R2Settings:PublicUrl"] = "https://" + Host,
        ["ImageGeneratorSettings:MongoDatabase"] = "imagino_staging",
        ["R2Settings:BucketName"] = "imagino-images-staging",
        ["R2Settings:BucketNameVideos"] = "imagino-videos-staging"
    };
    private static IConfiguration Config(Dictionary<string, string?> settings) =>
        new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

    [Fact]
    public void DefaultsDoNotPermitStagingStorage()
    {
        Assert.DoesNotContain(Host, StagingWebhookFixtures.ReplicateHosts(null));
        var values = Settings(); values["Webhooks:StagingFixturesEnabled"] = "false";
        Assert.Throws<ArgumentException>(() => RemoteUrlPolicy.Validate("https://" + Host + "/fixture.png",
            StagingWebhookFixtures.ReplicateHosts(Config(values))));
    }

    [Fact]
    public void OnlyExactStagingHostIsPermitted()
    {
        var hosts = StagingWebhookFixtures.ReplicateHosts(Config(Settings()));
        Assert.Equal(Host, RemoteUrlPolicy.Validate("https://" + Host + "/fixture.png", hosts).Host);
        Assert.Throws<ArgumentException>(() => RemoteUrlPolicy.Validate("https://pub-00000000000000000000000000000000.r2.dev/fixture.png", hosts));
        Assert.Throws<ArgumentException>(() => RemoteUrlPolicy.Validate("http://" + Host + "/fixture.png", hosts));
    }

    [Theory]
    [InlineData("ImageGeneratorSettings:MongoDatabase", "imagino")]
    [InlineData("R2Settings:BucketName", "imagino-images")]
    [InlineData("R2Settings:BucketNameVideos", "imagino-videos")]
    [InlineData("R2Settings:PublicUrl", "https://example.com")]
    [InlineData("Webhooks:StagingFixtureHost", "*.r2.dev")]
    [InlineData("Webhooks:StagingFixtureHost", "example.com")]
    [InlineData("ReplicateSettings:ApiKey", "nonempty-test-key")]
    public void UnsafeFixtureConfigurationFailsClosed(string key, string value)
    {
        var values = Settings(); values[key] = value;
        Assert.Throws<InvalidOperationException>(() => StagingWebhookFixtures.ReplicateHosts(Config(values)));
    }
}
