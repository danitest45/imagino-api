namespace Imagino.Api.Security;

public sealed class SafeMediaDownloader(IHttpClientFactory clients)
{
    public async Task<byte[]> DownloadAsync(string url, string[] hosts, int maxBytes,
        string? googleApiKey = null, CancellationToken ct = default)
    {
        var uri = RemoteUrlPolicy.Validate(url, hosts);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromMinutes(2));
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        // Never send Google credentials to storage, delivery CDNs, or arbitrary output hosts.
        if (googleApiKey != null && uri.IdnHost == "generativelanguage.googleapis.com")
            request.Headers.Add("x-goog-api-key", googleApiKey);
        using var response = await clients.CreateClient("ProviderSecure").SendAsync(request,
            HttpCompletionOption.ResponseHeadersRead, deadline.Token);
        // Redirects deliberately fail closed. Add individual destinations only after staging verification.
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > maxBytes) throw new ArgumentException("Remote media exceeds size limit.");
        await using var source = await response.Content.ReadAsStreamAsync(deadline.Token);
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await source.ReadAsync(buffer, deadline.Token)) != 0)
        {
            if (output.Length + read > maxBytes) throw new ArgumentException("Remote media exceeds size limit.");
            await output.WriteAsync(buffer.AsMemory(0, read), deadline.Token);
        }
        return output.ToArray();
    }
}
