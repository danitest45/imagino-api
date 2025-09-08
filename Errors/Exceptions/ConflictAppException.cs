namespace Imagino.Api.Errors.Exceptions;

public class ConflictAppException : Exception
{
    public ConflictAppException(string message) : base(message) { }
}
