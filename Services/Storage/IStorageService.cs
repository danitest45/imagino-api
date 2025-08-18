namespace Imagino.Api.Services.Storage
{
    public interface IStorageService
    {
        Task<string> UploadAsync(Stream stream, string key, string contentType);
    }
}
