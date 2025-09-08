namespace Imagino.Api.Errors.Exceptions;

public class ValidationAppException : Exception
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationAppException(IDictionary<string, string[]> errors)
        : base("Validation failed.")
    {
        Errors = errors;
    }
}
