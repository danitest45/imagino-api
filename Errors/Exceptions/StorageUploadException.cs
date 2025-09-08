namespace Imagino.Api.Errors.Exceptions;

public class StorageUploadException : Exception
{
    public string Key { get; }
    public string Reason { get; }

    public StorageUploadException(string key, string reason)
        : base($"Failed to upload '{key}'.")
    {
        Key = key;
        Reason = reason;
    }
}
