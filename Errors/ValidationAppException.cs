namespace Imagino.Api.Errors;

public class ValidationAppException : Exception
{
    public object? Meta { get; }
    public ValidationAppException(string message, object? meta = null) : base(message)
    {
        Meta = meta;
    }
}
