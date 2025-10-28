using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using Imagino.Api.Models.Image;

namespace Imagino.Api.DTOs.Image
{
    public class ImageModelVersionWebhookConfigDto
    {
        public string? Url { get; set; }

        public List<string>? Events { get; set; }
    }

    public class ImageModelVersionLimitsDto
    {
        public int? MaxWidth { get; set; }
        public int? MaxHeight { get; set; }
        public List<string>? Formats { get; set; }
    }

    public class ImageModelVersionRolloutDto
    {
        [Range(0, 100)]
        public int? CanaryPercent { get; set; }
    }

    public class CreateImageModelVersionDto
    {
        [Required]
        public string ModelId { get; set; } = string.Empty;

        [Required]
        public string VersionTag { get; set; } = string.Empty;

        [Required]
        public string EndpointUrl { get; set; } = string.Empty;

        public ImageModelVersionWebhookConfigDto? WebhookConfig { get; set; }

        public JsonDocument? ParamSchema { get; set; }

        public JsonDocument? Defaults { get; set; }

        public ImageModelVersionLimitsDto? Limits { get; set; }

        public ImageModelPricingDto? Pricing { get; set; }

        [Required]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ImageModelVersionStatus Status { get; set; } = ImageModelVersionStatus.Active;

        public ImageModelVersionRolloutDto? Rollout { get; set; }

        public string? ReleaseNotes { get; set; }
    }

    public class UpdateImageModelVersionDto : CreateImageModelVersionDto
    {
    }

    public class ImageModelVersionDto : CreateImageModelVersionDto
    {
        public string? Id { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
