using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace Imagino.Api.Models
{
    public class VideoJob
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("prompt")]
        public string? Prompt { get; set; }

        [BsonElement("modelSlug")]
        public string? ModelSlug { get; set; }

        [BsonElement("versionTag")]
        public string? VersionTag { get; set; }

        [BsonElement("presetId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? PresetId { get; set; }

        [BsonElement("resolvedParams")]
        [JsonIgnore]
        public BsonDocument? ResolvedParams { get; set; }

        [BsonElement("jobId")]
        public string? JobId { get; set; }

        [BsonElement("providerJobId")]
        public string? ProviderJobId { get; set; }

        [BsonElement("userId")]
        public string? UserId { get; set; }

        [BsonElement("status")]
        [BsonRepresentation(BsonType.String)]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public VideoJobStatus Status { get; set; }

        [BsonElement("videoUrl")]
        public string? VideoUrl { get; set; }

        [BsonElement("durationSeconds")]
        public int DurationSeconds { get; set; }

        [BsonElement("tokenConsumed")]
        public bool TokenConsumed { get; set; }

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("errorMessage")]
        public string? ErrorMessage { get; set; }
    }
}
