using Imagino.Api.Repository;
using Imagino.Api.Services;
using Imagino.Api.Services.ImageGeneration;
using Imagino.Api.Services.WebhookImage;
using Imagino.Api.Settings;

namespace Imagino.Api.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddAppServices(this IServiceCollection services, IConfiguration configuration)
        {

            services.AddHttpClient<JobsService, JobsService>();
            services.AddTransient<IJobsService, JobsService>();
            services.AddTransient<IImageJobRepository, ImageJobRepository>();
            services.AddTransient<IReplicateJobsService, ReplicateJobsService>();
            services.AddTransient<IWebhookImageService, WebhookImageService>();
            services.AddTransient<IUserRepository, UserRepository>();
            services.AddTransient<IUserService, UserService>();
            services.AddTransient<IJwtService, JwtService>();


            return services;
        }
    }
}
