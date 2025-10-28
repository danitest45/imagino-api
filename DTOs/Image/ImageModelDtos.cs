using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Imagino.Api.Models.Image;

namespace Imagino.Api.DTOs.Image
{
    public class ImageModelCapabilitiesDto
    {
        public bool Image { get; set; }
        public bool Inpaint { get; set; }
        public bool Upscale { get; set; }
    }

    public class ImageModelPricingDto
    {
        [Range(1, int.MaxValue)]
        public int CreditsPerImage { get; set; }

        [Range(0, int.MaxValue)]
        public int MinCredits { get; set; }
    }

    public class CreateImageModelDto
    {
        [Required]
        [RegularExpression("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
        public string Slug { get; set; } = string.Empty;

        [Required]
        public string DisplayName { get; set; } = string.Empty;

        [Required]
        public string ProviderId { get; set; } = string.Empty;

        [Required]
        public ImageModelCapabilitiesDto Capabilities { get; set; } = new();

        [Required]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ImageModelVisibility Visibility { get; set; } = ImageModelVisibility.Public;

        [Required]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ImageModelStatus Status { get; set; } = ImageModelStatus.Active;

        public string? DefaultVersionId { get; set; }

        [Required]
        public ImageModelPricingDto Pricing { get; set; } = new();

        public List<string>? Tags { get; set; }
    }

    public class UpdateImageModelDto : CreateImageModelDto
    {
    }

    public class ImageModelDto : CreateImageModelDto
    {
        public string? Id { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
