using Imagino.Api.Repository;
using Imagino.Api.Services;
using Imagino.Api.Repositories.Image;
using Imagino.Api.Repository.Image;
using Imagino.Api.Services.Image;
using Imagino.Api.Services.ImageGeneration;
using Imagino.Api.Services.WebhookImage;
using Imagino.Api.Services.Storage;
using Imagino.Api.Services.Billing;
using Imagino.Api.Settings;
using Imagino.Api.Services;

namespace Imagino.Api.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddAppServices(this IServiceCollection services, IConfiguration configuration)
        {

            services.Configure<R2StorageSettings>(configuration.GetSection("R2Settings"));
            services.Configure<EmailSettings>(configuration.GetSection("Email"));

            services.AddHttpClient<JobsService, JobsService>();
            services.AddTransient<IJobsService, JobsService>();
            services.AddTransient<IImageJobRepository, ImageJobRepository>();
            services.AddTransient<IImageModelProviderRepository, ImageModelProviderRepository>();
            services.AddTransient<IImageModelRepository, ImageModelRepository>();
            services.AddTransient<IImageModelVersionRepository, ImageModelVersionRepository>();
            services.AddTransient<IImageModelPresetRepository, ImageModelPresetRepository>();
            services.AddTransient<IModelResolverService, ModelResolverService>();
            services.AddTransient<IImageJobCreationService, ImageJobCreationService>();
            services.AddTransient<ImageCatalogSeeder>();
            services.AddHttpClient<ReplicateJobsService, ReplicateJobsService>();
            services.AddTransient<IReplicateJobsService, ReplicateJobsService>();
            services.AddHttpClient("ImageModelProvider");
            services.AddTransient<IWebhookImageService, WebhookImageService>();
            services.AddTransient<IUserRepository, UserRepository>();
            services.AddTransient<IUserService, UserService>();
            services.AddTransient<IBillingService, BillingService>();
            services.AddTransient<IStripeEventRepository, StripeEventRepository>();
            services.AddTransient<IJwtService, JwtService>();
            services.AddTransient<IRefreshTokenRepository, RefreshTokenRepository>();
            services.AddTransient<IEmailTokenRepository, EmailTokenRepository>();
            services.AddSingleton<IStorageService, R2StorageService>();
            services.AddHttpClient<IEmailSender, ResendEmailSender>();


            return services;
        }
    }
}
