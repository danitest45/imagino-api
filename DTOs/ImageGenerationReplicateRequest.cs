namespace Imagino.Api.DTOs
{
    public class ImageGenerationReplicateRequest
    {
        public string? Prompt { get; set; }
        public int QualityLevel { get; set; } = 3; // 1 (rápido) até 5 (qualidade máxima)
        public string AspectRatio { get; set; } = "1:1";
    }
}
