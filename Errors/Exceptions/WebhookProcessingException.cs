namespace Imagino.Api.Errors.Exceptions;

public class WebhookProcessingException : Exception
{
    public string EventId { get; }

    public WebhookProcessingException(string eventId, string message)
        : base(message)
    {
        EventId = eventId;
    }
}
