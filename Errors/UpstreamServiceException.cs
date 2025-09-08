namespace Imagino.Api.Errors;

public class UpstreamServiceException : Exception
{
    public string Provider { get; }
    public string? ProviderCode { get; }
    public int? Status { get; }

    public UpstreamServiceException(string provider, string? providerCode = null, int? status = null, string message = "Upstream service error") : base(message)
    {
        Provider = provider;
        ProviderCode = providerCode;
        Status = status;
    }
}
