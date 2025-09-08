namespace Imagino.Api.Errors.Exceptions;

public class NotFoundAppException : Exception
{
    public NotFoundAppException(string message) : base(message) { }
}
