namespace Imagino.Api.DTOs
{
    public class ReplicateWebhookRequest
    {
        public string Id { get; set; }
        public string Status { get; set; }
        public string? Output { get; set; }
        public Dictionary<string, object>? Input { get; set; }
        public string? Logs { get; set; }
        public Dictionary<string, object>? Metrics { get; set; }
        public Dictionary<string, string>? Urls { get; set; }
    }
}
