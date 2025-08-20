namespace Imagino.Api.Services.Storage
{
    public interface IStorageService
    {
        Task<string> UploadAsync(Stream stream, string key, string contentType);
        string GetPresignedDownloadUrl(string key, string fileName, string contentType = "image/png");
    }
}
