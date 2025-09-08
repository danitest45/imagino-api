namespace Imagino.Api.Errors;

public class StripeServiceException : Exception
{
    public string? StripeCode { get; }
    public StripeServiceException(string message, string? stripeCode = null) : base(message)
    {
        StripeCode = stripeCode;
    }
}
