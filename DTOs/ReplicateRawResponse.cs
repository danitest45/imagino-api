namespace Imagino.Api.DTOs
{
    public class ReplicateRawResponse
    {
        public string? id { get; set; }
        public string? status { get; set; }
        public object? output { get; set; } // ainda pode ser útil, mesmo sendo null aqui
        public ReplicateUrls urls { get; set; }
        public DateTime createdAt { get; set; }
        public DateTime completedAt { get; set; }
    }
    public class ReplicateUrls
    {
        public string get { get; set; }
        public string cancel { get; set; }
        public string stream { get; set; }
        public string web { get; set; }
    }
}
