namespace Imagino.Api.Models
{
    public class RunPodRawResponse
    {
        public bool Success { get; set; }
        public string Content { get; set; } = string.Empty;
        public List<string>? Errors { get; set; }
    }
}
