namespace Imagino.Api.Models
{
    public class ImageRequest
    {
        public string Prompt { get; set; } = string.Empty;
        public string? NegativePrompt { get; set; }
        public string? Model { get; set; }
        public string? Lora { get; set; }
        public int Width { get; set; } = 512;
        public int Height { get; set; } = 512;
    }
}
