using MongoDB.Bson.Serialization.Attributes;
namespace Imagino.Api.Models;
[BsonIgnoreExtraElements]
public class StripeEventRecord
{
    [BsonId] public string Id { get; set; } = default!;
    public string EventId { get; set; } = default!;
    public DateTime Created { get; set; }
    public string Type { get; set; } = default!;
    public string Status { get; set; } = "processing";
    public string? ClaimId { get; set; }
    public DateTime LeaseUntil { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public int Attempts { get; set; } = 1;
}
