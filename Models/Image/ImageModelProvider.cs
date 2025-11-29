using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Imagino.Api.Models.Image
{
    [BsonIgnoreExtraElements]
    public class ImageModelProvider
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = default!;

        [BsonElement("name")]
        public string Name { get; set; } = default!;

        [BsonElement("status")]
        public string Status { get; set; } = "Active";

        [BsonElement("providerType")]
        [BsonRepresentation(BsonType.String)]
        public ImageProviderType ProviderType { get; set; } = ImageProviderType.Replicate;

        [BsonElement("notes")]
        public string? Notes { get; set; }

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; }

        [BsonElement("updatedAt")]
        public DateTime UpdatedAt { get; set; }
    }
}
