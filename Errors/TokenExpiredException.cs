namespace Imagino.Api.Errors;

public class TokenExpiredException : Exception
{
    public TokenExpiredException(string message = "Token expired") : base(message) { }
}
