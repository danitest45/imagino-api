using System.Net.Http.Json;
using System.Text.Json;
using Imagino.Api.Security;

namespace Imagino.Api.Services.Generation;

public sealed class GenerationProviderHttp(IHttpClientFactory clients)
{
    // This client removes HttpClient URL/body logging: polling/delivery URLs may be signed.
    public async Task<JsonDocument> SendAsync(HttpMethod method, string url, string header, string key,
        object? body, string[] allowedHosts, CancellationToken ct, Action<int>? observeStatus = null, string? apiVersion = null)
    {
        var uri = RemoteUrlPolicy.Validate(url, allowedHosts);
        using var request = new HttpRequestMessage(method, uri);
        request.Headers.Add(header, key);
        if (apiVersion != null) request.Headers.Add("X-Runway-Version", apiVersion);
        if (body != null) request.Content = JsonContent.Create(body);
        using var response = await clients.CreateClient("GenerationPrivate").SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        observeStatus?.Invoke((int)response.StatusCode);
        if (!response.IsSuccessStatusCode)
        {
            string? code = null;
            if (apiVersion != null)
            {
                try {
                    using var error = JsonDocument.Parse(await ReadBoundedAsync(response.Content, 65536, ct));
                    if (error.RootElement.TryGetProperty("reason", out var reason) && reason.ValueKind == JsonValueKind.String &&
                        System.Text.RegularExpressions.Regex.IsMatch(reason.GetString()!, "^[A-Z0-9_.]{1,100}$")) code = reason.GetString();
                } catch (JsonException) { }
            }
            var delay = response.Headers.RetryAfter?.Delta?.TotalSeconds ??
                (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow)?.TotalSeconds;
            throw new ProviderCallException((int)response.StatusCode, code, delay);
        }
        var bytes = await ReadBoundedAsync(response.Content, 29 * 1024 * 1024, ct);
        return JsonDocument.Parse(bytes);
    }
    public async Task<byte[]> DownloadAsync(string url, string[] allowedHosts, string? googleKey, int maxBytes, CancellationToken ct, string? expectedContentType = null,
        Action<string?>? observeContentType = null)
    {
        var uri = RemoteUrlPolicy.Validate(url, allowedHosts);
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        if (uri.IdnHost == "generativelanguage.googleapis.com" && googleKey != null) request.Headers.Add("x-goog-api-key", googleKey);
        using var response = await clients.CreateClient("GenerationPrivate").SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode) throw new ProviderCallException((int)response.StatusCode);
        observeContentType?.Invoke(response.Content.Headers.ContentType?.MediaType);
        if (expectedContentType != null && response.Content.Headers.ContentType?.MediaType != expectedContentType)
            throw new InvalidDataException("Provider output Content-Type differs from the expected container.");
        return await ReadBoundedAsync(response.Content, maxBytes, ct);
    }
    private static async Task<byte[]> ReadBoundedAsync(HttpContent content, int max, CancellationToken ct)
    {
        if (content.Headers.ContentLength > max) throw new InvalidDataException("Provider response exceeds limit.");
        await using var stream = await content.ReadAsStreamAsync(ct);
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(buffer, ct)) > 0)
        {
            if (output.Length + read > max) throw new InvalidDataException("Provider response exceeds limit.");
            await output.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        return output.ToArray();
    }
}
public sealed class ProviderCallException(int status, string? code = null, double? retryAfterSeconds = null) : Exception("Provider HTTP request failed.")
{
    public int Status { get; } = status;
    public string? Code { get; } = code;
    public double? RetryAfterSeconds { get; } = retryAfterSeconds;
}
