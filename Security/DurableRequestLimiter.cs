using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Imagino.Api.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Imagino.Api.Security;

public sealed record RequestLimit(int Count, int WindowSeconds);
public static class LaunchRequestLimits
{
    public static RequestLimit? For(string path, string method) => path switch {
        "/api/auth/login" => new(20, 900),
        "/api/auth/register" or "/api/auth/password/forgot" or "/api/auth/resend-verification" => new(10, 3600),
        "/api/auth/password/reset" or "/api/auth/verify-email" => new(20, 900),
        "/api/auth/refresh" => new(120, 900),
        "/api/auth/google/login" or "/api/auth/google/callback" => new(30, 900),
        "/api/generation/quote" => new(30, 60),
        "/api/generation/jobs" when method == "POST" => new(5, 60),
        _ when path.StartsWith("/api/generation/jobs", StringComparison.Ordinal) && (path.EndsWith("/download") || path.EndsWith("/media")) => new(60, 60),
        _ when path.StartsWith("/api/generation/jobs", StringComparison.Ordinal) && method == "GET" => new(180, 60),
        _ when path.Contains("profile-image", StringComparison.Ordinal) || path.EndsWith("/uploads", StringComparison.Ordinal) => new(10, 60),
        _ => null
    };
}
[BsonIgnoreExtraElements]
public sealed class RequestLimitCounter
{
    [BsonId] public string Id { get; set; } = "";
    public long Count { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}
public interface IRequestLimiter
{
    Task<bool> AllowAsync(string identity, RequestLimit limit, CancellationToken ct);
}
public sealed class DurableRequestLimiter : IRequestLimiter
{
    private readonly IMongoCollection<RequestLimitCounter> counters;
    private readonly SemaphoreSlim indexGate = new(1, 1);
    private volatile bool indexesReady;
    public DurableRequestLimiter(IMongoClient client, IOptions<ImageGeneratorSettings> options)
    {
        counters = client.GetDatabase(options.Value.MongoDatabase).GetCollection<RequestLimitCounter>("request_limits_v1");
    }
    public async Task<bool> AllowAsync(string identity, RequestLimit limit, CancellationToken ct)
    {
        if (!indexesReady)
        {
            await indexGate.WaitAsync(ct);
            try {
                if (!indexesReady) {
                    await counters.Indexes.CreateOneAsync(new CreateIndexModel<RequestLimitCounter>(
                        Builders<RequestLimitCounter>.IndexKeys.Ascending(c => c.ExpiresAtUtc), new CreateIndexOptions { ExpireAfter = TimeSpan.Zero, Name = "request_limit_expiry" }), cancellationToken: ct);
                    indexesReady = true;
                }
            } finally { indexGate.Release(); }
        }
        var now = DateTime.UtcNow;
        var window = new DateTimeOffset(now).ToUnixTimeSeconds() / limit.WindowSeconds;
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity + "/" + window))).ToLowerInvariant();
        var filter = Builders<RequestLimitCounter>.Filter.Eq(c => c.Id, id);
        var update = Builders<RequestLimitCounter>.Update.Inc(c => c.Count, 1)
            .SetOnInsert(c => c.ExpiresAtUtc, now.AddSeconds(limit.WindowSeconds * 2));
        RequestLimitCounter result;
        try {
            result = await counters.FindOneAndUpdateAsync(filter, update,
                new FindOneAndUpdateOptions<RequestLimitCounter> { IsUpsert = true, ReturnDocument = ReturnDocument.After }, ct);
        } catch (MongoCommandException e) when (e.Code == 11000) {
            result = await counters.FindOneAndUpdateAsync(filter, update,
                new FindOneAndUpdateOptions<RequestLimitCounter> { ReturnDocument = ReturnDocument.After }, ct);
        }
        return result.Count <= limit.Count;
    }
}
public sealed class LaunchRequestMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IRequestLimiter limiter, IConfiguration config)
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var failed = false;
        try { await InvokeCoreAsync(context, limiter, config); }
        catch { failed = true; throw; }
        finally {
            var group = RequestGroup(context.Request.Path.Value?.ToLowerInvariant() ?? "");
            Imagino.Api.Services.Generation.GenerationTelemetry.Requests.Add(1, new("group", group), new("status", failed ? 500 : context.Response.StatusCode));
            Imagino.Api.Services.Generation.GenerationTelemetry.RequestSeconds.Record(System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalSeconds, new KeyValuePair<string, object?>("group", group));
        }
    }
    private static string RequestGroup(string path)
    {
        if (path.StartsWith("/api/generation/jobs/")) return path.EndsWith("/media") || path.EndsWith("/download") ? "/api/generation/media" : "/api/generation/job";
        if (path.StartsWith("/api/users/") && path.EndsWith("/profile-image")) return "/api/users/profile-image";
        if (path.StartsWith("/api/auth/") && LaunchRequestLimits.For(path, "GET") != null || path is "/api/generation/quote" or "/api/generation/jobs") return path;
        return "other";
    }
    private async Task InvokeCoreAsync(HttpContext context, IRequestLimiter limiter, IConfiguration config)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
        if (context.Request.Method == "OPTIONS") { await next(context); return; }
        // Cookie-auth actions require a browser origin. Non-browser operators use an
        // explicit allowlisted Origin too; CORS alone does not enforce CSRF.
        if (context.Request.Method == "POST" && path is "/api/auth/refresh" or "/api/auth/logout")
        {
            var origin = context.Request.Headers.Origin.ToString();
            var allowed = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
            if (context.Request.Cookies.ContainsKey("refreshToken") && !allowed.Contains(origin, StringComparer.Ordinal))
            { context.Response.StatusCode = 403; await context.Response.WriteAsJsonAsync(new { code = "ORIGIN_REJECTED" }); return; }
        }
        var limit = LaunchRequestLimits.For(path, context.Request.Method);
        if (limit == null || path.StartsWith("/api/generation") && context.User.Identity?.IsAuthenticated != true)
        { await next(context); return; }
        var owner = context.User.FindFirstValue("sub") ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var scope = RequestGroup(path);
        try
        {
            var ipLimit = owner == null ? limit : limit with { Count = limit.Count * 5 };
            var allowed = await limiter.AllowAsync(scope + "/ip/" + ip, ipLimit, context.RequestAborted);
            if (allowed && owner != null) allowed = await limiter.AllowAsync(scope + "/owner/" + owner, limit, context.RequestAborted);
            if (allowed && owner == null && context.Request.ContentType?.StartsWith("application/json") == true && context.Request.ContentLength is > 0 and <= 16384)
            {
                context.Request.EnableBuffering();
                try {
                    using var body = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
                    if (body.RootElement.TryGetProperty("email", out var email) && email.ValueKind == JsonValueKind.String)
                        allowed = await limiter.AllowAsync(path + "/identity/" + email.GetString()?.Trim().ToLowerInvariant(), limit with { Count = Math.Min(10, limit.Count) }, context.RequestAborted);
                } catch (JsonException) { /* controller handles malformed requests */ }
                finally { context.Request.Body.Position = 0; }
            }
            if (!allowed) {
                if (path is "/api/auth/password/forgot" or "/api/auth/resend-verification")
                { context.Response.StatusCode = 200; return; }
                context.Response.StatusCode = 429; context.Response.Headers.RetryAfter = limit.WindowSeconds.ToString();
                context.Response.Headers.CacheControl = "no-store";
                await context.Response.WriteAsJsonAsync(new { code = "RATE_LIMITED", detail = "Too many requests. Please wait before trying again." }); return;
            }
        }
        catch (MongoException) { context.Response.StatusCode = 503; await context.Response.WriteAsJsonAsync(new { code = "REQUEST_GUARD_UNAVAILABLE" }); return; }
        await next(context);
    }
}
