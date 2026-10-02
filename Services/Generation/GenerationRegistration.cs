using Imagino.Api.Security;

namespace Imagino.Api.Services.Generation;

public static class GenerationRegistration
{
    public static void AddGenerationV2(this IServiceCollection services, IConfiguration config)
    {
        ValidateStaging(config);
        services.Configure<GenerationSettings>(config.GetSection("GenerationV2"));
        services.AddSingleton<IGenerationRepository, MongoGenerationRepository>();
        services.AddSingleton<GenerationService>();
        services.AddSingleton<GenerationProcessor>();
        services.AddSingleton<IGenerationOutputStore, GenerationOutputStore>();
        services.AddSingleton<GenerationProviderHttp>();
        services.AddSingleton<IGenerationProvider, BflGenerationProvider>();
        services.AddSingleton<IGenerationProvider, GeminiImageGenerationProvider>();
        services.AddSingleton<IGenerationProvider, VeoGenerationProvider>();
        services.AddSingleton<IGenerationProvider, StagingGenerationProvider>();
        services.AddHttpClient("GenerationPrivate", c => c.Timeout = TimeSpan.FromMinutes(2))
            .ConfigurePrimaryHttpMessageHandler(RemoteUrlPolicy.CreateHandler)
            .RemoveAllLoggers();
        services.AddHostedService<GenerationWorker>();
    }
    public static void ValidateStaging(IConfiguration config)
    {
        var enabled = config.GetValue<bool>("GenerationV2:Enabled");
        var seed = config.GetValue<bool>("GenerationV2:SeedStagingCatalog");
        var fixture = config.GetValue<bool>("GenerationV2:StagingFixtureEnabled");
        var paid = config.GetValue<bool>("GenerationV2:PaidGenerationEnabled");
        if ((seed || fixture || paid) && !enabled) throw new InvalidOperationException("GenerationV2 must be enabled explicitly.");
        if (!enabled) return;
        if (config["ImageGeneratorSettings:MongoDatabase"] != "imagino_staging" ||
            config["R2Settings:BucketName"] != "imagino-images-staging" ||
            config["R2Settings:BucketNameVideos"] != "imagino-videos-staging")
            throw new InvalidOperationException("GenerationV2 is restricted to the isolated Imagino staging database and buckets in this phase.");
    }
}
