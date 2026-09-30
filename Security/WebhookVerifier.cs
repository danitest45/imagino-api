using System.Security.Cryptography;
using System.Text;

namespace Imagino.Api.Security;

public static class WebhookVerifier
{
    public static bool Verify(ReadOnlySpan<byte> body, string? id, string? timestamp, string? signatures,
        string? secret, DateTimeOffset now)
    {
        if (body.Length > 16 * 1024 * 1024 || string.IsNullOrEmpty(id) || id.Length > 200 ||
            string.IsNullOrEmpty(signatures) || signatures.Length > 2048 ||
            string.IsNullOrEmpty(secret) || !long.TryParse(timestamp, out var seconds) ||
            seconds < now.ToUnixTimeSeconds() - 300 || seconds > now.ToUnixTimeSeconds() + 300) return false;
        try
        {
            var key = Convert.FromBase64String(secret.StartsWith("whsec_") ? secret[6..] : secret);
            var prefix = Encoding.UTF8.GetBytes($"{id}.{timestamp}.");
            var data = new byte[prefix.Length + body.Length];
            prefix.CopyTo(data, 0); body.CopyTo(data.AsSpan(prefix.Length));
            var expected = HMACSHA256.HashData(key, data);
            return signatures.Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(s =>
            {
                var parts = s.Split(',');
                if (parts.Length != 2 || parts[0] != "v1") return false;
                try { return CryptographicOperations.FixedTimeEquals(expected, Convert.FromBase64String(parts[1])); }
                catch (FormatException) { return false; }
            });
        }
        catch (FormatException) { return false; }
    }
}
