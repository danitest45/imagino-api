namespace Imagino.Api.DTOs
{
    public class ImageGenerationReplicateRequest
    {
        public string? Prompt { get; set; }
        public int QualityLevel { get; set; } = 3;
        public string AspectRatio { get; set; } = "1:1";
        public string ModelId { get; set; } = "flux-dev";
    }
}
