using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Imagino.Api.Models.Video;

namespace Imagino.Api.DTOs.Video
{
    public class CreateVideoModelProviderDto
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public VideoProviderType ProviderType { get; set; }

        public Dictionary<string, string>? Config { get; set; }
    }

    public class UpdateVideoModelProviderDto : CreateVideoModelProviderDto
    {
    }

    public class VideoModelProviderDto : CreateVideoModelProviderDto
    {
        public string? Id { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
