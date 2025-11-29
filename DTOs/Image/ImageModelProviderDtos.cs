using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Imagino.Api.Models.Image;

namespace Imagino.Api.DTOs.Image
{
    public class CreateImageModelProviderDto
    {
        [Required]
        public string Name { get; set; } = default!;

        [Required]
        public string Status { get; set; } = "Active";

        [Required]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ImageProviderType ProviderType { get; set; } = ImageProviderType.Replicate;

        public string? Notes { get; set; }
    }

    public class UpdateImageModelProviderDto
    {
        public string? Name { get; set; }

        public string? Status { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ImageProviderType? ProviderType { get; set; }

        public string? Notes { get; set; }
    }

    public class ImageModelProviderDto : CreateImageModelProviderDto
    {
        public string? Id { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
