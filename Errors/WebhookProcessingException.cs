namespace Imagino.Api.Errors;

public class WebhookProcessingException : Exception
{
    public WebhookProcessingException(string message = "Webhook processing failed") : base(message) { }
}
