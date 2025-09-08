using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Imagino.Api.Models
{
    public class EmailToken
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public string UserId { get; set; } = default!;
        public string Purpose { get; set; } = default!; // verify_email | reset_password
        public string TokenHash { get; set; } = default!;
        public DateTime ExpiresAt { get; set; }
        public DateTime? ConsumedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedIp { get; set; }
        public DateTime? LastSentAt { get; set; }
    }
}
