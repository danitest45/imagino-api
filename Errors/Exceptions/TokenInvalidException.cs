namespace Imagino.Api.Errors.Exceptions;

public class TokenInvalidException : Exception
{
    public TokenInvalidException() : base("Token invalid.") { }
}
