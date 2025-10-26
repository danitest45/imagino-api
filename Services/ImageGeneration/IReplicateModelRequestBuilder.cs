using Imagino.Api.DTOs;

namespace Imagino.Api.Services.ImageGeneration
{
    public interface IReplicateModelRequestBuilder
    {
        /// <summary>
        /// Nome usado para identificar o modelo (ex.: "flux", "seedream-4").
        /// </summary>
        string ModelKey { get; }

        /// <summary>
        /// Constrói o payload de entrada para a API da Replicate.
        /// </summary>
        /// <param name="request">Dados fornecidos pelo cliente.</param>
        /// <param name="webhookUrl">URL do webhook configurado.</param>
        /// <returns>Objeto anônimo com o payload para a Replicate.</returns>
        object BuildRequest(ImageGenerationReplicateRequest request, string? webhookUrl);
    }
}
