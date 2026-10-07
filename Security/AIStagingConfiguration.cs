using Imagino.Api.Services.Generation;

namespace Imagino.Api.Security;

public static class AIStagingConfiguration
{
    public const string Preview = "https://imagino-front-git-codex-imagino-ai-776a34-danitest45s-projects.vercel.app";
    public const string WorkingStudioPreview = "https://imagino-front-git-feat-imagino-work-16a0f7-danitest45s-projects.vercel.app";
    public const string CreativeHubPreview = "https://imagino-front-git-feat-imagino-crea-9cdb36-danitest45s-projects.vercel.app";
    public static void Validate(IConfiguration config)
    {
        foreach (var section in new[] { "Stripe", "Google", "Resend", "ReplicateSettings", "GeminiSettings", "VeoSettings" })
            if (config.GetSection(section).GetChildren().Any()) throw new InvalidOperationException("AI staging forbids configuration for optional external integrations.");
        var settings = config.GetSection("GenerationV2").Get<GenerationSettings>() ?? new();
        if (!settings.Enabled || !settings.SeedStagingCatalog || !settings.StagingFixtureEnabled ||
            !string.IsNullOrEmpty(settings.GeminiApiKey) ||
            (!settings.BflHomologationEnabled && !string.IsNullOrEmpty(settings.BflApiKey)) ||
            (!settings.OpenAiHomologationEnabled && !string.IsNullOrEmpty(settings.OpenAiApiKey)) ||
            (settings.PaidGenerationEnabled && !settings.BflHomologationEnabled && !settings.OpenAiHomologationEnabled))
            throw new InvalidOperationException("AI staging permits only synthetic generation or finite BFL/OpenAI authorizations; other provider keys are forbidden.");
        if (settings.OpenAiSingleSmokeEnabled && (!settings.OpenAiHomologationEnabled ||
            OpenAiSingleSmokePolicy.ProjectionUsd >= OpenAiSingleSmokePolicy.ObservedCeilingUsd / 2))
            throw new InvalidOperationException("Single Flare smoke requires its isolated provider and comfortable cost projection.");
        if (settings.OpenAiHomologationEnabled && settings.PaidGenerationEnabled && !settings.OpenAiSingleSmokeEnabled && !OpenAiHomologationPolicy.CostBoundsVerified)
            throw new InvalidOperationException("OpenAI paid authorization requires verified complete cost bounds.");
        if ((settings.BflHomologationEnabled || settings.OpenAiHomologationEnabled) && (config["RENDER_SERVICE_ID"] != BflHomologationPolicy.ServiceId ||
            config["RENDER_GIT_BRANCH"] != "codex/imagino-ai-revival-v2" ||
            config["RENDER_EXTERNAL_HOSTNAME"] != "imagino-api-ai-staging.onrender.com"))
            throw new InvalidOperationException("Paid homologation is restricted to its exact AI staging service and branch.");
        GenerationRegistration.ValidateStaging(config);
        if (settings.StagingFixtureDelaySeconds is < 0 or > 120)
            throw new InvalidOperationException("Synthetic staging delay must be between 0 and 120 seconds.");
        foreach (var key in new[] { "ImageGeneratorSettings:MongoConnection", "Jwt:Issuer", "Jwt:Audience", "R2Settings:AccessKeyId", "R2Settings:SecretAccessKey" })
            if (string.IsNullOrWhiteSpace(config[key])) throw new InvalidOperationException("AI staging requires " + key);
        if (System.Text.Encoding.UTF8.GetByteCount(config["Jwt:Secret"] ?? "") < 32)
            throw new InvalidOperationException("AI staging JWT secret must have at least 32 bytes.");
        var allowedOrigins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (config["Frontend:BaseUrl"] != Preview || allowedOrigins.Length != 3 ||
            !new HashSet<string>(allowedOrigins, StringComparer.Ordinal).SetEquals(new[] { Preview, WorkingStudioPreview, CreativeHubPreview }))
            throw new InvalidOperationException("AI staging requires exactly its three authorized Preview origins and the existing frontend base URL.");
        if (config["R2Settings:PublicUrl"] != "https://pub-56f86851d1884a3b8e7a73f1624e4239.r2.dev" ||
            config["R2Settings:ServiceUrl"] != "https://f3915d7185410d3a7d3a9599e22194af.r2.cloudflarestorage.com")
            throw new InvalidOperationException("AI staging requires its existing R2 staging endpoint.");
        if (config.GetValue<bool>("RefreshTokenCookie:Secure") != true || config.GetValue<bool>("RefreshTokenCookie:HttpOnly") != true ||
            config["RefreshTokenCookie:SameSite"] != "None" || !string.IsNullOrEmpty(config["RefreshTokenCookie:Domain"]))
            throw new InvalidOperationException("AI staging requires a secure host-only refresh cookie.");
    }
}
