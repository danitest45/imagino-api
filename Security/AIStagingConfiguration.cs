using Imagino.Api.Services.Generation;

namespace Imagino.Api.Security;

public static class AIStagingConfiguration
{
    public const string Preview = "https://imagino-front-git-codex-imagino-ai-776a34-danitest45s-projects.vercel.app";
    public static void Validate(IConfiguration config)
    {
        foreach (var section in new[] { "Stripe", "Google", "Resend", "ReplicateSettings", "GeminiSettings", "VeoSettings" })
            if (config.GetSection(section).GetChildren().Any()) throw new InvalidOperationException("AI staging forbids configuration for optional external integrations.");
        var settings = config.GetSection("GenerationV2").Get<GenerationSettings>() ?? new();
        if (!settings.Enabled || !settings.SeedStagingCatalog || !settings.StagingFixtureEnabled || settings.PaidGenerationEnabled ||
            !string.IsNullOrEmpty(settings.BflApiKey) || !string.IsNullOrEmpty(settings.GeminiApiKey))
            throw new InvalidOperationException("AI staging requires fixture-only Generation V2 with paid generation disabled and no provider keys.");
        GenerationRegistration.ValidateStaging(config);
        foreach (var key in new[] { "ImageGeneratorSettings:MongoConnection", "Jwt:Issuer", "Jwt:Audience", "R2Settings:AccessKeyId", "R2Settings:SecretAccessKey" })
            if (string.IsNullOrWhiteSpace(config[key])) throw new InvalidOperationException("AI staging requires " + key);
        if (System.Text.Encoding.UTF8.GetByteCount(config["Jwt:Secret"] ?? "") < 32)
            throw new InvalidOperationException("AI staging JWT secret must have at least 32 bytes.");
        if (config["Frontend:BaseUrl"] != Preview ||
            !(config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? []).SequenceEqual(new[] { Preview }))
            throw new InvalidOperationException("AI staging requires its exact authorized Preview origin.");
        if (config["R2Settings:PublicUrl"] != "https://pub-56f86851d1884a3b8e7a73f1624e4239.r2.dev" ||
            config["R2Settings:ServiceUrl"] != "https://f3915d7185410d3a7d3a9599e22194af.r2.cloudflarestorage.com")
            throw new InvalidOperationException("AI staging requires its existing R2 staging endpoint.");
        if (config.GetValue<bool>("RefreshTokenCookie:Secure") != true || config.GetValue<bool>("RefreshTokenCookie:HttpOnly") != true ||
            config["RefreshTokenCookie:SameSite"] != "None" || !string.IsNullOrEmpty(config["RefreshTokenCookie:Domain"]))
            throw new InvalidOperationException("AI staging requires a secure host-only refresh cookie.");
    }
}
