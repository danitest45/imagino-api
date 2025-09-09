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
        public string Username { get; set; } = default!;
        public string? PhoneNumber { get; set; }

        public bool EmailVerified { get; set; } = false;
        public DateTime? VerifiedAt { get; set; }

        public string? StripeCustomerId { get; set; }
        public string? StripeSubscriptionId { get; set; }
        public string? Plan { get; set; }
        public string? SubscriptionStatus { get; set; }
        public DateTimeOffset? CurrentPeriodEnd { get; set; }

        [BsonRepresentation(BsonType.String)]
        public SubscriptionType Subscription { get; set; } = SubscriptionType.Free;
        public int Credits { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
