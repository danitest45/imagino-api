namespace Imagino.Api.Errors.Exceptions;

public class WebhookSignatureException : Exception
{
    public WebhookSignatureException() : base("Webhook signature invalid.") { }
}
