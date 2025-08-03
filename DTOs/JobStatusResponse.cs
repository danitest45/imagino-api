namespace Imagino.Api.DTOs
{
    public class JobStatusResponse
    {
        public string? JobId { get; set; }
        public string? Status { get; set; }
        public string? ImageUrl { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
