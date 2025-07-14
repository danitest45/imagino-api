using Imagino.Api.Models;

namespace Imagino.Api.Repository
{
    public interface IImageJobRepository
    {
        Task InsertAsync(ImageJob job);
    }
}