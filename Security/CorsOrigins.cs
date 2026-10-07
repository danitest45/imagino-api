namespace Imagino.Api.Security;

public static class CorsOrigins
{
    public static bool Matches(string? origin, string[] allowed, bool exact = false) =>
        exact ? allowed.Contains(origin, StringComparer.Ordinal) :
        Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.UserInfo.Length == 0 &&
        allowed.Any(value => Uri.TryCreate(value, UriKind.Absolute, out var candidate) &&
            candidate.UserInfo.Length == 0 && !candidate.Host.Contains('*') &&
            candidate.Scheme == uri.Scheme && candidate.IdnHost == uri.IdnHost && candidate.Port == uri.Port);
}
