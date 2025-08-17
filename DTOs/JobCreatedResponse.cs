namespace Imagino.Api.DTOs
{
    public class JobCreatedResponse
    {
        public string? JobId { get; set; }
        public string? Status { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
