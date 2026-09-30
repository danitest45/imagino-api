using Google.Apis.Auth;
using Imagino.Api.Security;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace Imagino.Api.Services;

public record GoogleIdentity(string Subject, string Email, bool EmailVerified);
public interface IGoogleOAuthClient
{
    Task<GoogleIdentity?> ExchangeAsync(string code, string verifier, string nonce);
}

public sealed class GoogleOAuthClient(IHttpClientFactory clients, IConfiguration config) : IGoogleOAuthClient
{
    public async Task<GoogleIdentity?> ExchangeAsync(string code, string verifier, string nonce)
    {
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code, ["client_id"] = config["Google:ClientId"]!,
            ["client_secret"] = config["Google:ClientSecret"]!, ["redirect_uri"] = config["Google:RedirectUri"]!,
            ["grant_type"] = "authorization_code", ["code_verifier"] = verifier
        });
        using var response = await clients.CreateClient("ProviderSecure").PostAsync("https://oauth2.googleapis.com/token", form);
        if (!response.IsSuccessStatusCode) return null;
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var idToken = body.RootElement.GetProperty("id_token").GetString();
        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { config["Google:ClientId"]! }
            });
            // Read nonce only after signature, issuer, expiry and audience validation.
            if (!HasExpectedNonce(idToken!, nonce)) return null;
            if (!payload.EmailVerified || string.IsNullOrWhiteSpace(payload.Subject) || string.IsNullOrWhiteSpace(payload.Email)) return null;
            return new GoogleIdentity(payload.Subject, payload.Email, payload.EmailVerified);
        }
        catch (InvalidJwtException) { return null; }
    }

    public static bool HasExpectedNonce(string validatedIdToken, string expected)
    {
        using var claims = JsonDocument.Parse(Base64UrlEncoder.Decode(validatedIdToken.Split('.')[1]));
        return claims.RootElement.TryGetProperty("nonce", out var nonce) && TokenSecurity.Equal(nonce.GetString(), expected);
    }
}
