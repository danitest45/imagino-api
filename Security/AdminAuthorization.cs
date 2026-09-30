using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Imagino.Api.Security;

public static class AdminAuthorization
{
    public const string Policy = "AdminOnly";

    public static bool IsConfiguredAdmin(ClaimsPrincipal user, IConfiguration configuration)
    {
        var userId = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId)) return false;

        var adminUserIds = configuration.GetSection("Admin:UserIds").Get<string[]>()
            ?? Array.Empty<string>();

        return adminUserIds.Contains(userId, StringComparer.Ordinal);
    }
}
