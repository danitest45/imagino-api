using System;

namespace Imagino.Api.DTOs.Video
{
    public class VideoJobStatusResponse
    {
        public string? JobId { get; set; }
        public string? Status { get; set; }
        public string? VideoUrl { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int DurationSeconds { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
