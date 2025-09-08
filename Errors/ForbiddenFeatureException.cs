namespace Imagino.Api.Errors;

public class ForbiddenFeatureException : Exception
{
    public ForbiddenFeatureException(string message = "Feature not allowed") : base(message) { }
}
