using Imagino.Api.Services.ImageGeneration;
using Imagino.Api.Settings;

namespace Imagino.Api.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddAppServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<ImageGeneratorSettings>(
                configuration.GetSection("ImageGenerator")
            );

            services.AddHttpClient<IImageGenerationService, ImageGenerationService>();

            return services;
        }
    }
}
