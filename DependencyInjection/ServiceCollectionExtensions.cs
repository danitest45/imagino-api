using Imagino.Api.Repository;
using Imagino.Api.Repositories.Image;
using Imagino.Api.Repository.Image;
using Imagino.Api.Services;
using Imagino.Api.Services.Image;
using Imagino.Api.Services.Image.Providers;
using Imagino.Api.Services.WebhookImage;
using Imagino.Api.Services.Storage;
using Imagino.Api.Services.Billing;
using Imagino.Api.Settings;

namespace Imagino.Api.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddAppServices(this IServiceCollection services, IConfiguration configuration)
        {

            services.Configure<R2StorageSettings>(configuration.GetSection("R2Settings"));
            services.Configure<EmailSettings>(configuration.GetSection("Email"));

            services.AddTransient<IImageJobRepository, ImageJobRepository>();
            services.AddTransient<IImageModelProviderRepository, ImageModelProviderRepository>();
            services.AddTransient<IImageModelRepository, ImageModelRepository>();
            services.AddTransient<IImageModelVersionRepository, ImageModelVersionRepository>();
            services.AddTransient<IImageModelPresetRepository, ImageModelPresetRepository>();
            services.AddTransient<IModelResolverService, ModelResolverService>();
            services.AddTransient<IImageJobCreationService, ImageJobCreationService>();
            services.AddHttpClient("ImageModelProvider");
            services.AddSingleton<IImageProviderClient, ReplicateImageProviderClient>();
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
