namespace Imagino.Api.DTOs
{
    public class ImageGenerationRequest
    {
        public string Prompt { get; set; } = string.Empty;
        public string NegativePrompt { get; set; } = string.Empty;
        public int Steps { get; set; } = 30;
        public int Width { get; set; } = 512;
        public int Height { get; set; } = 512;
        public double CfgScale { get; set; } = 7.0;
        public string SamplerName { get; set; } = "Euler";
    }
}
