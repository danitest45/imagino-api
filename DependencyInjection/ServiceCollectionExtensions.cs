using Imagino.Api.Repository;
using Imagino.Api.Services;
using Imagino.Api.Services.ImageGeneration;
using Imagino.Api.Services.WebhookImage;
using Imagino.Api.Services.Storage;
using Imagino.Api.Settings;

namespace Imagino.Api.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddAppServices(this IServiceCollection services, IConfiguration configuration)
        {

            services.Configure<R2StorageSettings>(configuration.GetSection("R2Settings"));

            services.AddHttpClient<JobsService, JobsService>();
            services.AddTransient<IJobsService, JobsService>();
            services.AddTransient<IImageJobRepository, ImageJobRepository>();
            services.AddHttpClient<ReplicateJobsService, ReplicateJobsService>();
            services.AddTransient<IReplicateJobsService, ReplicateJobsService>();
            services.AddTransient<IWebhookImageService, WebhookImageService>();
            services.AddTransient<IUserRepository, UserRepository>();
            services.AddTransient<IUserService, UserService>();
            services.AddTransient<IJwtService, JwtService>();
            services.AddTransient<IRefreshTokenRepository, RefreshTokenRepository>();
            services.AddSingleton<IStorageService, R2StorageService>();


            return services;
        }
    }
}
