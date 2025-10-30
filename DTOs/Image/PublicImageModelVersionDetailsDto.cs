using System.Text.Json;
using Imagino.Api.Models.Image;

namespace Imagino.Api.DTOs.Image
{
    public class PublicImageModelVersionDetailsDto
    {
        public string ModelSlug { get; set; } = string.Empty;
        public string VersionTag { get; set; } = string.Empty;
        public ImageModelVersionStatus Status { get; set; }
        public JsonDocument? ParamSchema { get; set; }
        public JsonDocument? Defaults { get; set; }
        public ImageModelVersionLimitsDto? Limits { get; set; }
        public ImageModelPricingDto? Pricing { get; set; }
        public string? ReleaseNotes { get; set; }
    }
}
