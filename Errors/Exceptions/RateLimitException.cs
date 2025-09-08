namespace Imagino.Api.Errors.Exceptions;

public class RateLimitException : Exception
{
    public int? RetryAfterSeconds { get; }

    public RateLimitException(string message, int? retryAfterSeconds = null)
        : base(message)
    {
        RetryAfterSeconds = retryAfterSeconds;
    }
}
