using Imagino.Api.Repository;
using Imagino.Api.Services.ImageGeneration;
using Imagino.Api.Services.WebhookImage;
using Imagino.Api.Settings;

namespace Imagino.Api.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddAppServices(this IServiceCollection services, IConfiguration configuration)
        {

            services.AddHttpClient<JobsService>();
            services.AddTransient<IJobsService, JobsService>();
            services.AddTransient<IImageJobRepository, ImageJobRepository>();
            services.AddTransient<IWebhookImageService, WebhookImageService>();



            return services;
        }
    }
}
