using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Imagino.Api.Models.Image
{
    public enum ImageModelProviderStatus
    {
        Active,
        Disabled
    }

    public enum ImageModelProviderAuthMode
    {
        SecretRef,
        Encrypted
    }

    public class ImageModelProviderAuth
    {
        [BsonElement("mode")]
        [BsonRepresentation(BsonType.String)]
        public ImageModelProviderAuthMode Mode { get; set; }

        [BsonElement("secretRef")]
        public string? SecretRef { get; set; }

        [BsonElement("encBlob")]
        public string? EncBlob { get; set; }

        [BsonElement("encKeyId")]
        public string? EncKeyId { get; set; }

        [BsonElement("header")]
        public string Header { get; set; } = "Authorization";

        [BsonElement("scheme")]
        public string? Scheme { get; set; }

        [BsonElement("baseUrl")]
        public string? BaseUrl { get; set; }

        [BsonElement("notes")]
        public string? Notes { get; set; }
    }

    public class ImageModelProvider
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("name")]
        public string Name { get; set; } = string.Empty;

        [BsonElement("status")]
        [BsonRepresentation(BsonType.String)]
        public ImageModelProviderStatus Status { get; set; } = ImageModelProviderStatus.Active;

        [BsonElement("auth")]
        public ImageModelProviderAuth Auth { get; set; } = new();

        [BsonElement("webhookRef")]
        public string? WebhookRef { get; set; }

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
