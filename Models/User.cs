// Models/User.cs
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace Imagino.Api.Models
{
    public class User
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public string? Email { get; set; }
        public string? PasswordHash { get; set; }
        public string? GoogleId { get; set; }
        public string? ProfileImageUrl { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
