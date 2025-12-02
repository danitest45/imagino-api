using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Imagino.Api.Models.Video
{
    public enum VideoModelVisibility
    {
        Public,
        Premium,
        Internal
    }

    public enum VideoModelStatus
    {
        Active,
        Deprecated,
        Archived
    }

    public class VideoModelCapabilities
    {
        [BsonElement("video")]
        public bool Video { get; set; }
    }

    public class VideoModelPricing
    {
        [BsonElement("creditsPerSecond")]
        public int CreditsPerSecond { get; set; }

        [BsonElement("creditsPerVideo")]
        public int CreditsPerVideo { get; set; }

        [BsonElement("minCredits")]
        public int MinCredits { get; set; }
    }

    public class VideoModel
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
        public VideoModelCapabilities Capabilities { get; set; } = new();

        [BsonElement("visibility")]
        [BsonRepresentation(BsonType.String)]
        public VideoModelVisibility Visibility { get; set; } = VideoModelVisibility.Public;

        [BsonElement("status")]
        [BsonRepresentation(BsonType.String)]
        public VideoModelStatus Status { get; set; } = VideoModelStatus.Active;

        [BsonElement("defaultVersionId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? DefaultVersionId { get; set; }

        [BsonElement("pricing")]
        public VideoModelPricing Pricing { get; set; } = new();

        [BsonElement("tags")]
        public List<string>? Tags { get; set; }

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
