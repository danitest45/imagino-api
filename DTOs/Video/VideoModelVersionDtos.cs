using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using Imagino.Api.Models.Video;

namespace Imagino.Api.DTOs.Video
{
    public class VideoModelVersionLimitsDto
    {
        public int? MaxDurationSeconds { get; set; }
        public List<string>? Resolutions { get; set; }
    }

    public class VideoModelVersionRolloutDto
    {
        public int? CanaryPercent { get; set; }
    }

    public class VideoModelVersionDto
    {
        public string? Id { get; set; }
        public string ModelId { get; set; } = string.Empty;
        public string VersionTag { get; set; } = string.Empty;
        public string EndpointUrl { get; set; } = string.Empty;
        public JsonDocument? ParamSchema { get; set; }
        public JsonDocument? Defaults { get; set; }
        public VideoModelVersionLimitsDto? Limits { get; set; }
        public VideoModelPricingDto? Pricing { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public VideoModelVersionStatus Status { get; set; }

        public VideoModelVersionRolloutDto? Rollout { get; set; }
        public string? ReleaseNotes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class CreateVideoModelVersionDto
    {
        [Required]
        public string ModelId { get; set; } = string.Empty;

        [Required]
        public string VersionTag { get; set; } = string.Empty;

        public string EndpointUrl { get; set; } = string.Empty;

        public JsonDocument? ParamSchema { get; set; }

        public JsonDocument? Defaults { get; set; }

        public VideoModelVersionLimitsDto? Limits { get; set; }

        public VideoModelPricingDto? Pricing { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public VideoModelVersionStatus Status { get; set; } = VideoModelVersionStatus.Active;

        public VideoModelVersionRolloutDto? Rollout { get; set; }

        public string? ReleaseNotes { get; set; }
    }

    public class UpdateVideoModelVersionDto : CreateVideoModelVersionDto
    {
    }
}
