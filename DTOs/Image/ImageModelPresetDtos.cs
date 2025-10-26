using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using Imagino.Api.Models.Image;

namespace Imagino.Api.DTOs.Image
{
    public class CreateImageModelPresetDto
    {
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

        [Url]
        public string? Thumbnail { get; set; }

        public JsonDocument? Params { get; set; }

        public List<string>? Locks { get; set; }

        [Required]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ImageModelVisibility Visibility { get; set; } = ImageModelVisibility.Public;

        [Required]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ImageModelPresetStatus Status { get; set; } = ImageModelPresetStatus.Active;

        public int Ordering { get; set; }
    }

    public class UpdateImageModelPresetDto : CreateImageModelPresetDto
    {
    }

    public class ImageModelPresetDto : CreateImageModelPresetDto
    {
        public string? Id { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
