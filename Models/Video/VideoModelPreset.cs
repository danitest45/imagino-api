using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Imagino.Api.Models.Video
{
    public enum VideoModelPresetStatus
    {
        Draft,
        Active,
        Inactive,
        Archived
    }

    public class VideoModelPreset
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("modelId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string ModelId { get; set; } = string.Empty;

        [BsonElement("modelVersionId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string ModelVersionId { get; set; } = string.Empty;

        [BsonElement("slug")]
        public string Slug { get; set; } = string.Empty;

        [BsonElement("name")]
        public string Name { get; set; } = string.Empty;

        [BsonElement("description")]
        public string? Description { get; set; }

        [BsonElement("status")]
        [BsonRepresentation(BsonType.String)]
        public VideoModelPresetStatus Status { get; set; } = VideoModelPresetStatus.Draft;

        [BsonElement("params")]
        public BsonDocument? Params { get; set; }

        [BsonElement("locks")]
        public List<string>? Locks { get; set; }

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
