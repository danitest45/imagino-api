namespace Imagino.Api.DTOs
{
    /// <summary>
    /// Represents a request to generate an image using a text prompt and custom settings.
    /// </summary>
    public class ImageGenerationRunPodRequest
    {
        /// <summary>
        /// The main prompt describing what the image should contain.
        /// </summary>
        public string Prompt { get; set; } = string.Empty;

        /// <summary>
        /// Text describing what should be excluded from the image.
        /// </summary>
        public string NegativePrompt { get; set; } = string.Empty;

        /// <summary>
        /// The number of inference steps to use during image generation (higher = more detail).
        /// </summary>
        public int Steps { get; set; } = 30;

        /// <summary>
        /// The width of the output image in pixels.
        /// </summary>
        public int Width { get; set; } = 512;

        /// <summary>
        /// The height of the output image in pixels.
        /// </summary>
        public int Height { get; set; } = 512;

        /// <summary>
        /// The CFG (Classifier-Free Guidance) scale — controls prompt adherence (usually 1–20).
        /// </summary>
        public double CfgScale { get; set; } = 7.0;

        /// <summary>
        /// The name of the sampler to use for the generation (e.g., Euler, DDIM, DPM++ 2M).
        /// </summary>
        public string SamplerName { get; set; } = "Euler";
    }
}
