using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Imagino.Api.Models
{
    public class ImageJob
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("prompt")]
        public string? Prompt { get; set; }

        [BsonElement("jobId")]
        public string? JobId { get; set; }

        [BsonElement("userId")]
        public string? UserId { get; set; }

        [BsonElement("status")]
        public string? Status { get; set; }

        [BsonElement("imageUrls")]
        public List<string> ImageUrls { get; set; } = new();

        [BsonElement("tokenConsumed")]
        public bool TokenConsumed { get; set; } = false;

        [BsonElement("aspectRatio")]
        public string AspectRatio { get; set; } = string.Empty;

        [BsonElement("model")]
        public string Model { get; set; } = string.Empty;

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
