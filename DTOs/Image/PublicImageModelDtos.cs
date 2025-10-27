using System.Collections.Generic;
using Imagino.Api.Models.Image;

namespace Imagino.Api.DTOs.Image
{
    public class PublicImageModelPresetSummaryDto
    {
        public string Id { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Thumbnail { get; set; }
        public int Ordering { get; set; }
        public ImageModelVisibility Visibility { get; set; } = ImageModelVisibility.Public;
    }

    public class PublicImageModelVersionSummaryDto
    {
        public string VersionTag { get; set; } = string.Empty;
        public ImageModelVersionStatus Status { get; set; }
    }

    public class PublicImageModelSummaryDto
    {
        public string Slug { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public ImageModelCapabilities Capabilities { get; set; } = new();
        public ImageModelVisibility Visibility { get; set; }
        public ImageModelStatus Status { get; set; }
        public string? DefaultVersionTag { get; set; }
        public List<PublicImageModelPresetSummaryDto>? Presets { get; set; }
    }

    public class PublicImageModelDetailsDto : PublicImageModelSummaryDto
    {
        public List<PublicImageModelVersionSummaryDto>? Versions { get; set; }
    }
}
