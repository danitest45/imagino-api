namespace Imagino.Api.Security;

public static class StartupConfiguration
{
    public static void Validate(IConfiguration config, bool development)
    {
        var errors = new List<string>();
        if (!string.IsNullOrWhiteSpace(config["Auth:RefreshTokensValidAfter"]) &&
            !DateTimeOffset.TryParse(config["Auth:RefreshTokensValidAfter"], out _))
            errors.Add("Auth:RefreshTokensValidAfter must be a UTC timestamp");
        foreach (var key in new[] { "ImageGeneratorSettings:MongoConnection", "ImageGeneratorSettings:MongoDatabase", "Jwt:Secret", "Jwt:Issuer", "Jwt:Audience", "Frontend:BaseUrl" })
            if (string.IsNullOrWhiteSpace(config[key])) errors.Add(key + " is required");
        if (System.Text.Encoding.UTF8.GetByteCount(config["Jwt:Secret"] ?? "") < 32) errors.Add("Jwt:Secret must have at least 32 bytes");
        if (!Uri.TryCreate(config["Frontend:BaseUrl"], UriKind.Absolute, out var frontend) ||
            frontend.UserInfo.Length != 0 || (!development && frontend.Scheme != "https")) errors.Add("Frontend:BaseUrl must be a trusted HTTPS origin");
        var cookie = config.GetSection("RefreshTokenCookie").Get<Settings.RefreshTokenCookieSettings>() ?? new();
        if (!cookie.HttpOnly || (!development && !cookie.Secure) || !Enum.TryParse<SameSiteMode>(cookie.SameSite, true, out _) || cookie.ExpiresDays is < 1 or > 30)
            errors.Add("RefreshTokenCookie requires HttpOnly, Secure and valid SameSite/ExpiresDays");
        foreach (var group in new[] { new[] { "Google:ClientId", "Google:ClientSecret", "Google:RedirectUri" }, StripeKeys, new[] { "R2Settings:AccessKeyId", "R2Settings:SecretAccessKey", "R2Settings:ServiceUrl", "R2Settings:BucketName" } })
        {
            // An optional integration may be disabled, but never partially configured.
            // Google has no harmless default metadata: any configured key requires the complete group.
            var activationKeys = group[0] is "Google:ClientId" or "Stripe:ApiKey" ? group : group.Take(2);
            var enabled = activationKeys.Any(k => !string.IsNullOrWhiteSpace(config[k]));
            if (enabled) foreach (var key in group) if (string.IsNullOrWhiteSpace(config[key])) errors.Add(key + " is required for its integration");
        }
        if (StripeKeys.Any(k => !string.IsNullOrWhiteSpace(config[k])))
        {
            var staging = config["ImageGeneratorSettings:MongoDatabase"] == "imagino_staging";
            if (staging && !(config["Stripe:ApiKey"] ?? "").StartsWith("sk_test_", StringComparison.Ordinal) &&
                !(config["Stripe:ApiKey"] ?? "").StartsWith("rk_test_", StringComparison.Ordinal))
                errors.Add("Stripe:ApiKey must be a TEST key in staging");
            foreach (var key in StripeKeys.Skip(4))
            {
                if (!Uri.TryCreate(config[key], UriKind.Absolute, out var url) || url.UserInfo.Length != 0 ||
                    (!development && url.Scheme != "https") || url.Fragment.Length != 0 ||
                    (staging && (frontend == null || url.GetLeftPart(UriPartial.Authority) != frontend.GetLeftPart(UriPartial.Authority))))
                    errors.Add(key + " must use the trusted frontend origin and HTTPS outside Development");
            }
            if (staging && (frontend?.Host != "imagino-front-git-fix-revival-opera-a2526a-danitest45s-projects.vercel.app" ||
                config.GetValue<int?>("Stripe:CreditsPro") is not (null or 100) ||
                config.GetValue<int?>("Stripe:CreditsUltra") is not (null or 300)))
                errors.Add("Staging billing requires the authorized Preview and credit fixtures");
        }
        if (new[] { "ReplicateSettings:ApiKey", "ReplicateSettings:WebhookUrl" }.Any(k => !string.IsNullOrWhiteSpace(config[k])) &&
            string.IsNullOrWhiteSpace(config["Webhooks:ReplicateSigningSecret"]))
            errors.Add("Webhooks:ReplicateSigningSecret is required for Replicate");
        // Fail startup if the optional synthetic-fixture configuration escapes staging.
        StagingWebhookFixtures.ReplicateHosts(config);
        if (errors.Count != 0) throw new InvalidOperationException("Invalid configuration: " + string.Join("; ", errors));
    }
    public static readonly string[] StripeKeys = ["Stripe:ApiKey", "Stripe:WebhookSecret", "Stripe:PricePro", "Stripe:PriceUltra",
        "Stripe:SuccessUrl", "Stripe:CancelUrl", "Stripe:PortalReturnUrl"];
}
