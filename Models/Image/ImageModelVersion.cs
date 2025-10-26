using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Imagino.Api.Models.Image
{
    public enum ImageModelVersionStatus
    {
        Active,
        Canary,
        Deprecated,
        Archived
    }

    public class ImageModelVersionWebhookConfig
    {
        [BsonElement("url")]
        public string? Url { get; set; }

        [BsonElement("events")]
        public List<string>? Events { get; set; }
    }

    public class ImageModelVersionLimits
    {
        [BsonElement("maxWidth")]
        public int? MaxWidth { get; set; }

        [BsonElement("maxHeight")]
        public int? MaxHeight { get; set; }

        [BsonElement("formats")]
        public List<string>? Formats { get; set; }
    }

    public class ImageModelVersionRollout
    {
        [BsonElement("canaryPercent")]
        public int? CanaryPercent { get; set; }
    }

    public class ImageModelVersion
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("modelId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string ModelId { get; set; } = string.Empty;

        [BsonElement("versionTag")]
        public string VersionTag { get; set; } = string.Empty;

        [BsonElement("endpointUrl")]
        public string EndpointUrl { get; set; } = string.Empty;

        [BsonElement("webhookConfig")]
        public ImageModelVersionWebhookConfig? WebhookConfig { get; set; }

        [BsonElement("paramSchema")]
        public BsonDocument? ParamSchema { get; set; }

        [BsonElement("defaults")]
        public BsonDocument? Defaults { get; set; }

        [BsonElement("limits")]
        public ImageModelVersionLimits? Limits { get; set; }

        [BsonElement("pricing")]
        public ImageModelPricing? Pricing { get; set; }

        [BsonElement("status")]
        [BsonRepresentation(BsonType.String)]
        public ImageModelVersionStatus Status { get; set; } = ImageModelVersionStatus.Active;

        [BsonElement("rollout")]
        public ImageModelVersionRollout? Rollout { get; set; }

        [BsonElement("releaseNotes")]
        public string? ReleaseNotes { get; set; }

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
