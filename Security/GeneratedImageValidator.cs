namespace Imagino.Api.Security;

public static class GeneratedImageValidator
{
    public const int MaxBytes = 20 * 1024 * 1024;
    public const int MaxBase64Chars = ((MaxBytes + 2) / 3) * 4;
    // RunPod's signed JSON envelope carries base64 rather than a binary download.
    public const int MaxWebhookBodyBytes = MaxBase64Chars + 64 * 1024;

    public static (string Extension, string ContentType) Identify(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0 || bytes.Length > MaxBytes)
            throw new ArgumentException("Generated image must be between 1 byte and 20 MiB.");
        if (bytes.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            return (".png", "image/png");
        if (bytes.StartsWith(new byte[] { 255, 216, 255 }))
            return (".jpg", "image/jpeg");
        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8))
            return (".webp", "image/webp");
        throw new ArgumentException("Only PNG, JPEG and WebP generated images are accepted.");
    }
}
