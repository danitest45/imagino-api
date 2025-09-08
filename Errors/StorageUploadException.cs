namespace Imagino.Api.Errors;

public class StorageUploadException : Exception
{
    public string? Provider { get; }
    public string? ProviderCode { get; }
    public StorageUploadException(string message = "Storage upload failed", string? provider = null, string? providerCode = null) : base(message)
    {
        Provider = provider;
        ProviderCode = providerCode;
    }
}
