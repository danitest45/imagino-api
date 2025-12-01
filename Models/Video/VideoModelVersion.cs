using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Imagino.Api.Models.Video
{
    public enum VideoModelVersionStatus
    {
        Active,
        Canary,
        Deprecated,
        Archived
    }

    public class VideoModelVersionLimits
    {
        [BsonElement("maxDurationSeconds")]
        public int? MaxDurationSeconds { get; set; }

        [BsonElement("resolutions")]
        public List<string>? Resolutions { get; set; }
    }

    public class VideoModelVersionRollout
    {
        [BsonElement("canaryPercent")]
        public int? CanaryPercent { get; set; }
    }

    public class VideoModelVersion
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

        [BsonElement("paramSchema")]
        public BsonDocument? ParamSchema { get; set; }

        [BsonElement("defaults")]
        public BsonDocument? Defaults { get; set; }

        [BsonElement("limits")]
        public VideoModelVersionLimits? Limits { get; set; }

        [BsonElement("pricing")]
        public VideoModelPricing? Pricing { get; set; }

        [BsonElement("status")]
        [BsonRepresentation(BsonType.String)]
        public VideoModelVersionStatus Status { get; set; } = VideoModelVersionStatus.Active;

        [BsonElement("rollout")]
        public VideoModelVersionRollout? Rollout { get; set; }

        [BsonElement("releaseNotes")]
        public string? ReleaseNotes { get; set; }

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
