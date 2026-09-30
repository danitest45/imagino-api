using Microsoft.Extensions.Caching.Memory;

namespace Imagino.Api.Security;

public record GoogleOAuthTransaction(string State, string BrowserToken, string Nonce, string Verifier, DateTimeOffset ExpiresAt);

// Single-instance, short-lived transactions. Restart fails closed. Use a shared atomic
// store before scaling to multiple instances; never serialize credentials into cookies.
public sealed class GoogleOAuthState(IMemoryCache cache)
{
    private readonly object gate = new();
    public GoogleOAuthTransaction Begin()
    {
        var tx = new GoogleOAuthTransaction(TokenSecurity.RandomToken(), TokenSecurity.RandomToken(),
            TokenSecurity.RandomToken(), TokenSecurity.RandomToken(), DateTimeOffset.UtcNow.AddMinutes(10));
        cache.Set("oauth:" + tx.State, tx, tx.ExpiresAt);
        return tx;
    }

    public GoogleOAuthTransaction? Consume(string? state, string? browserToken)
    {
        if (string.IsNullOrWhiteSpace(state) || state.Length > 100 || browserToken == null) return null;
        lock (gate)
        {
            var tx = cache.Get<GoogleOAuthTransaction>("oauth:" + state);
            if (tx == null || tx.ExpiresAt <= DateTimeOffset.UtcNow || !TokenSecurity.Equal(tx.BrowserToken, browserToken)) return null;
            cache.Remove("oauth:" + state);
            return tx;
        }
    }
}
