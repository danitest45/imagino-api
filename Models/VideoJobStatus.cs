using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;

namespace Imagino.Api.Models
{
    public enum VideoJobStatus
    {
        Created,
        Queued,
        Running,
        Completed,
        Failed
    }
}
