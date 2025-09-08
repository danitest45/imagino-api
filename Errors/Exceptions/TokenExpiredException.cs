namespace Imagino.Api.Errors.Exceptions;

public class TokenExpiredException : Exception
{
    public TokenExpiredException() : base("Token expired.") { }
}
