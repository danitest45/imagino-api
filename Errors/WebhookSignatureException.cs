namespace Imagino.Api.Errors;

public class WebhookSignatureException : Exception
{
    public WebhookSignatureException(string message = "Invalid webhook signature") : base(message) { }
}
