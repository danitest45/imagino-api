using Imagino.Api.Repository;
using Imagino.Api.Services.ImageGeneration;
using Imagino.Api.Settings;

namespace Imagino.Api.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddAppServices(this IServiceCollection services, IConfiguration configuration)
        {

            services.AddHttpClient<IImageGenerationService, ImageGenerationService>();
            services.AddTransient<IImageGenerationService, ImageGenerationService>();
            services.AddTransient<IImageJobRepository, ImageJobRepository>();


            return services;
        }
    }
}
