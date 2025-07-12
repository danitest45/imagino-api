using Imagino.Api.Settings;

namespace Imagino.Api.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddAppServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Bind settings
            services.Configure<ImageGeneratorSettings>(
                configuration.GetSection("ImageGenerator")
            );

            // Aqui depois podemos adicionar serviços, como o gerador de imagem
            // services.AddScoped<IImageService, ImageService>();

            return services;
        }
    }
}
