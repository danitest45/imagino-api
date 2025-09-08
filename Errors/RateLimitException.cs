namespace Imagino.Api.Errors;

public class RateLimitException : Exception
{
    public RateLimitException(string message = "Rate limit exceeded") : base(message) { }
}
