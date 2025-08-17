using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace Imagino.Api.Models
{
    public class RefreshToken
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string UserId { get; set; } = default!;

        public string Token { get; set; } = default!;
        public DateTime ExpiresAt { get; set; }
    }
}
