using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Imagino.Api.Models.Image
{
    public enum ImageModelVisibility
    {
        Public,
        Premium,
        Internal
    }

    public enum ImageModelStatus
    {
        Active,
        Deprecated,
        Archived
    }

    public class ImageModelCapabilities
    {
        [BsonElement("image")]
        public bool Image { get; set; }

        [BsonElement("inpaint")]
        public bool Inpaint { get; set; }

        [BsonElement("upscale")]
        public bool Upscale { get; set; }
    }

    public class ImageModelPricing
    {
        [BsonElement("creditsPerImage")]
        public int CreditsPerImage { get; set; }

        [BsonElement("minCredits")]
        public int MinCredits { get; set; }
    }

    public class ImageModel
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("slug")]
        public string Slug { get; set; } = string.Empty;

        [BsonElement("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [BsonElement("providerId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string ProviderId { get; set; } = string.Empty;

        [BsonElement("capabilities")]
        public ImageModelCapabilities Capabilities { get; set; } = new();

        [BsonElement("visibility")]
        [BsonRepresentation(BsonType.String)]
        public ImageModelVisibility Visibility { get; set; } = ImageModelVisibility.Public;

        [BsonElement("status")]
        [BsonRepresentation(BsonType.String)]
        public ImageModelStatus Status { get; set; } = ImageModelStatus.Active;

        [BsonElement("defaultVersionId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? DefaultVersionId { get; set; }

        [BsonElement("pricing")]
        public ImageModelPricing Pricing { get; set; } = new();

        [BsonElement("tags")]
        public List<string>? Tags { get; set; }

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
