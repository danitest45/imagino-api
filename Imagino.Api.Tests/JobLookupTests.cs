using Imagino.Api.Models;
using Imagino.Api.Repository;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Xunit;

namespace Imagino.Api.Tests;

public class JobLookupTests
{
    [Theory]
    [InlineData("not-an-object-id", false)]
    [InlineData("provider-job-123", false)]
    [InlineData("", false)]
    [InlineData("6abd000000000000000000a1", true)]
    public void LookupPreservesProviderIdsAndOnlySerializesValidObjectIds(string id, bool includesObjectId)
    {
        var image = ImageJobRepository.JobLookupFilter(id).Render(
            new RenderArgs<ImageJob>(BsonSerializer.SerializerRegistry.GetSerializer<ImageJob>(), BsonSerializer.SerializerRegistry));
        var video = VideoJobRepository.JobLookupFilter(id).Render(
            new RenderArgs<VideoJob>(BsonSerializer.SerializerRegistry.GetSerializer<VideoJob>(), BsonSerializer.SerializerRegistry));
        foreach (var filter in new[] { image, video })
        {
            var alternatives = filter["$or"].AsBsonArray;
            Assert.Equal(includesObjectId ? 3 : 2, alternatives.Count);
            Assert.Equal(id, alternatives[0].AsBsonDocument.GetElement(0).Value.AsString);
            Assert.Equal(id, alternatives[1].AsBsonDocument.GetElement(0).Value.AsString);
            if (includesObjectId) Assert.Equal(ObjectId.Parse(id), alternatives[2]["_id"].AsObjectId);
        }
    }
}
