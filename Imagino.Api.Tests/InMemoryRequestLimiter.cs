using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Imagino.Api.Security;

namespace Imagino.Api.Tests;

internal sealed class InMemoryRequestLimiter : IRequestLimiter
{
    private readonly ConcurrentDictionary<string, int> counts = new();
    public Task<bool> AllowAsync(string identity, RequestLimit limit, CancellationToken ct)
        => Task.FromResult(counts.AddOrUpdate(identity, 1, (_, n) => n + 1) <= limit.Count);
}
