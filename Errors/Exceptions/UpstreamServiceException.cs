namespace Imagino.Api.Errors.Exceptions;

public class UpstreamServiceException : Exception
{
    public string Provider { get; }
    public string? ProviderCode { get; }

    public UpstreamServiceException(string provider, string? providerCode, string message)
        : base(message)
    {
        Provider = provider;
        ProviderCode = providerCode;
    }
}
