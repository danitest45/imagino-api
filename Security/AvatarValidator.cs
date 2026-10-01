namespace Imagino.Api.Security;

public static class AvatarValidator
{
    public const int MaxBytes = 5 * 1024 * 1024;

    public static (string Extension, string ContentType) Identify(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0 || bytes.Length > MaxBytes)
            throw new ArgumentException("Avatar must be between 1 byte and 5 MiB.");
        if (bytes.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            return (".png", "image/png");
        if (bytes.StartsWith(new byte[] { 255, 216, 255 }))
            return (".jpg", "image/jpeg");
        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8))
            return (".webp", "image/webp");
        throw new ArgumentException("Only PNG, JPEG and WebP avatars are accepted.");
    }

    public static async Task<byte[]> ReadAsync(Stream stream, long declaredLength, CancellationToken ct = default)
    {
        if (declaredLength <= 0 || declaredLength > MaxBytes)
            throw new ArgumentException("Avatar must be between 1 byte and 5 MiB.");
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(buffer, ct)) != 0)
        {
            if (output.Length + read > MaxBytes) throw new ArgumentException("Avatar exceeds 5 MiB.");
            await output.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        var bytes = output.ToArray();
        Identify(bytes);
        return bytes;
    }
}
