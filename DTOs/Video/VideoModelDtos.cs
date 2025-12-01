using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Imagino.Api.Models.Video;

namespace Imagino.Api.DTOs.Video
{
    public class VideoModelCapabilitiesDto
    {
        public bool Video { get; set; }
    }

    public class VideoModelPricingDto
    {
        [Range(0, int.MaxValue)]
        public int CreditsPerSecond { get; set; }

        [Range(0, int.MaxValue)]
        public int CreditsPerVideo { get; set; }

        [Range(0, int.MaxValue)]
        public int MinCredits { get; set; }
    }

    public class CreateVideoModelDto
    {
        [Required]
        [RegularExpression("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
        public string Slug { get; set; } = string.Empty;

        [Required]
        public string DisplayName { get; set; } = string.Empty;

        [Required]
        public string ProviderId { get; set; } = string.Empty;

        [Required]
        public VideoModelCapabilitiesDto Capabilities { get; set; } = new();

        [Required]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public VideoModelVisibility Visibility { get; set; } = VideoModelVisibility.Public;

        [Required]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public VideoModelStatus Status { get; set; } = VideoModelStatus.Active;

        public string? DefaultVersionId { get; set; }

        [Required]
        public VideoModelPricingDto Pricing { get; set; } = new();

        public List<string>? Tags { get; set; }
    }

    public class UpdateVideoModelDto : CreateVideoModelDto
    {
    }

    public class VideoModelDto : CreateVideoModelDto
    {
        public string? Id { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
