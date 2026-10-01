using System.Net;
using System.Net.Sockets;

namespace Imagino.Api.Security;

public static class RemoteUrlPolicy
{
    public static Uri Validate(string value, params string[] hosts)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Port != 443 ||
            uri.UserInfo.Length != 0 || uri.Fragment.Length != 0 || IPAddress.TryParse(uri.Host, out _) ||
            !hosts.Any(h => string.Equals(uri.IdnHost, h, StringComparison.OrdinalIgnoreCase) ||
                h.StartsWith("*.") && uri.IdnHost.EndsWith(h[1..], StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Remote URL is not an allowed HTTPS destination.");
        return uri;
    }

    public static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address)) return false;
        var b = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
            return !(b[0] == 0 || b[0] == 10 || b[0] == 127 || b[0] >= 224 ||
                b[0] == 169 && b[1] == 254 || b[0] == 172 && b[1] >= 16 && b[1] <= 31 ||
                b[0] == 192 && (b[1] == 168 || b[1] == 0 || b[1] == 2) ||
                b[0] == 100 && b[1] >= 64 && b[1] <= 127 ||
                b[0] == 198 && (b[1] == 18 || b[1] == 19 || b[1] == 51) || b[0] == 203 && b[1] == 0);
        // Only global unicast IPv6, excluding documentation/transition ranges.
        return address.AddressFamily == AddressFamily.InterNetworkV6 && (b[0] & 0xe0) == 0x20 &&
            !(b[0] == 0x20 && b[1] == 0x01 && (b[2] == 0x0d && b[3] == 0xb8 || b[2] == 0 && b[3] == 0)) &&
            !(b[0] == 0x20 && b[1] == 0x02);
    }

    public static SocketsHttpHandler CreateHandler() => new()
    {
        AllowAutoRedirect = false, UseProxy = false, ConnectTimeout = TimeSpan.FromSeconds(15),
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        ConnectCallback = async (context, ct) =>
        {
            var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, ct);
            if (addresses.Length == 0 || addresses.Any(a => !IsPublicAddress(a)))
                throw new HttpRequestException("Remote DNS resolved to a prohibited address.");
            // Pin the socket to the vetted address; TLS still validates the original host.
            var socket = new Socket(addresses[0].AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(new IPEndPoint(addresses[0], context.DnsEndPoint.Port), ct);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch { socket.Dispose(); throw; }
        }
    };
}
