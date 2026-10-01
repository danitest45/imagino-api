using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Imagino.Api.Models;
using Imagino.Api.Repository;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;
using MongoDB.Driver.Core.Connections;
using MongoDB.Driver.Core.Servers;
using Moq;
using Xunit;
namespace Imagino.Api.Tests;
public class StripeJournalTests
{
    private static IAsyncCursor<T> Cursor<T>(params T[] items)
    {
        var cursor=new Mock<IAsyncCursor<T>>();
        cursor.SetupGet(c=>c.Current).Returns(items);
        cursor.SetupSequence(c=>c.MoveNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true).ReturnsAsync(false);
        return cursor.Object;
    }
    [Fact] public async Task MissingUniqueIndexFailsBeforeInsertion()
    {
        var collection=new Mock<IMongoCollection<StripeEventRecord>>();
        var indexes=new Mock<IMongoIndexManager<StripeEventRecord>>();
        indexes.Setup(i=>i.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(()=>Cursor(new BsonDocument("key",new BsonDocument("_id",1))));
        collection.SetupGet(c=>c.Indexes).Returns(indexes.Object);
        var repo=new StripeEventRepository(collection.Object);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>repo.TryClaimAsync(new StripeEventRecord {EventId="evt_fixture",ClaimId="claim"}));
        collection.Verify(c=>c.InsertOneAsync(It.IsAny<StripeEventRecord>(),It.IsAny<InsertOneOptions>(),It.IsAny<CancellationToken>()),Times.Never);
    }
    [Fact] public async Task FirstInsertUsesEventIdAsUniqueMongoIdentityAndLease()
    {
        var collection=new Mock<IMongoCollection<StripeEventRecord>>();
        var indexes=new Mock<IMongoIndexManager<StripeEventRecord>>();
        indexes.Setup(i=>i.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(()=>Cursor(
            new BsonDocument {{"key",new BsonDocument("EventId",1)},{"unique",true}}));
        collection.SetupGet(c=>c.Indexes).Returns(indexes.Object);
        collection.Setup(c=>c.InsertOneAsync(It.IsAny<StripeEventRecord>(),It.IsAny<InsertOneOptions>(),It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var repo=new StripeEventRepository(collection.Object);
        var row=new StripeEventRecord {EventId="evt_fixture",ClaimId="claim"};
        Assert.Equal(StripeClaim.Acquired,await repo.TryClaimAsync(row));
        Assert.Equal(row.EventId,row.Id);Assert.True(row.LeaseUntil>DateTime.UtcNow);
        collection.Verify(c=>c.InsertOneAsync(row,It.IsAny<InsertOneOptions>(),It.IsAny<CancellationToken>()),Times.Once);
    }
}
