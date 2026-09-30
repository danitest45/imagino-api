using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace Imagino.Api.Models
{
    [BsonIgnoreExtraElements]
    public class RefreshToken
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string UserId { get; set; } = default!;

        [BsonIgnoreIfNull]
        public string? Token { get; set; }
        [BsonIgnoreIfNull]
        public string? TokenHash { get; set; }
        public DateTime ExpiresAt { get; set; }
        // Legacy documents deserialize a missing timestamp as DateTime.MinValue.
        public DateTime CreatedAt { get; set; }
    }
}
