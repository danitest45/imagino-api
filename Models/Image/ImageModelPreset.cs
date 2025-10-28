using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Imagino.Api.Models.Image
{
    public enum ImageModelPresetStatus
    {
        Active,
        Deprecated
    }

    public class ImageModelPreset
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

        [BsonElement("thumbnail")]
        public string? Thumbnail { get; set; }

        [BsonElement("params")]
        public BsonDocument? Params { get; set; }

        [BsonElement("locks")]
        public List<string>? Locks { get; set; }

        [BsonElement("visibility")]
        [BsonRepresentation(BsonType.String)]
        public ImageModelVisibility Visibility { get; set; } = ImageModelVisibility.Public;

        [BsonElement("status")]
        [BsonRepresentation(BsonType.String)]
        public ImageModelPresetStatus Status { get; set; } = ImageModelPresetStatus.Active;

        [BsonElement("ordering")]
        public int Ordering { get; set; }

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
