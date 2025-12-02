using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using Imagino.Api.Models.Video;

namespace Imagino.Api.DTOs.Video
{
    public class VideoModelPresetDto
    {
        public string? Id { get; set; }

        [Required]
        public string ModelId { get; set; } = string.Empty;

        [Required]
        public string ModelVersionId { get; set; } = string.Empty;

        [Required]
        [RegularExpression("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
        public string Slug { get; set; } = string.Empty;

        [Required]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public VideoModelPresetStatus Status { get; set; } = VideoModelPresetStatus.Draft;

        public JsonDocument? Params { get; set; }

        public List<string>? Locks { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
