namespace Imagino.Api.DTOs
{
    public class JobStatusResponse
    {
        public string? JobId { get; set; }
        public string? Status { get; set; }
        public List<string> ImageUrls { get; set; } = new();
        public DateTime UpdatedAt { get; set; }
    }
}
