using System.Security.Cryptography;
using System.Text;
using Imagino.Api.Errors;

namespace Imagino.Api.Services.Generation;

public sealed class GenerationQuoteAuthorization(IConfiguration configuration)
{
    public string Issue(string owner, string quoteId) => quoteId + ":" + Signature(owner, quoteId);
    public string Verify(string owner, string? token)
    {
        var parts = token?.Split(':');
        if (parts?.Length != 3) throw new ConflictAppException("Quote expired or settings changed. Request a new quote.");
        var quoteId = parts[0] + ":" + parts[1];
        var expected = Encoding.UTF8.GetBytes(Signature(owner, quoteId));
        if (!CryptographicOperations.FixedTimeEquals(expected, Encoding.UTF8.GetBytes(parts[2])))
            throw new ConflictAppException("Quote expired or settings changed. Request a new quote.");
        return quoteId;
    }
    private string Signature(string owner, string quoteId)
    {
        var secret = configuration["Jwt:Secret"] ?? "";
        if (Encoding.UTF8.GetByteCount(secret) < 32) throw new InvalidOperationException("Quote signing configuration missing.");
        return Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret),
            Encoding.UTF8.GetBytes("imagino-generation-quote-v1\n" + owner + "\n" + quoteId))).ToLowerInvariant();
    }
}
