using System.Text.RegularExpressions;

namespace Imagino.Api.Security;

public static class StagingWebhookFixtures
{
    public static string[] ReplicateHosts(IConfiguration? config)
    {
        var hosts = new[] { "replicate.delivery", "*.replicate.delivery" };
        if (config?.GetValue<bool>("Webhooks:StagingFixturesEnabled") != true) return hosts;

        var host = config["Webhooks:StagingFixtureHost"] ?? "";
        // This opt-in is only for synthetic fixtures in the isolated staging resources.
        // Do not turn a configurable host into a general provider-output allowlist.
        if (!Regex.IsMatch(host, @"^pub-[a-f0-9]{32}\.r2\.dev$") ||
            config["R2Settings:PublicUrl"]?.TrimEnd('/') != "https://" + host ||
            config["ImageGeneratorSettings:MongoDatabase"] != "imagino_staging" ||
            config["R2Settings:BucketName"] != "imagino-images-staging" ||
            config["R2Settings:BucketNameVideos"] != "imagino-videos-staging" ||
            !string.IsNullOrWhiteSpace(config["ReplicateSettings:ApiKey"]))
            throw new InvalidOperationException("Staging webhook fixtures require isolated staging resources, the exact r2.dev public host, and no Replicate API key.");

        return hosts.Append(host).ToArray();
    }
}
