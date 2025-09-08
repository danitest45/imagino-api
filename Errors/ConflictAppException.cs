namespace Imagino.Api.Errors;

public class ConflictAppException : Exception
{
    public ConflictAppException(string message) : base(message) { }
}
