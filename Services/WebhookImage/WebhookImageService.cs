using System.Text.RegularExpressions;

namespace Imagino.Api.Services.WebhookImage
{
    public class WebhookImageService
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<WebhookImageService> _logger;

        public WebhookImageService(IWebHostEnvironment env, ILogger<WebhookImageService> logger)
        {
            _env = env;
            _logger = logger;
        }

        public async Task<string> SalvarImagemBase64Async(string base64Data, string jobId)
        {
            try
            {
                var base64 = Regex.Replace(base64Data, @"^data:image\/[a-zA-Z]+;base64,", string.Empty);
                var bytes = Convert.FromBase64String(base64);



                var nomeArquivo = $"{jobId}.png";

                // Retorna a URL pública
                return $"/images/{nomeArquivo}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao salvar imagem do job {JobId}", jobId);
                return null;
            }
        }
    }
}
