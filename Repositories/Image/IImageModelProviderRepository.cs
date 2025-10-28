using System.Collections.Generic;
using Imagino.Api.Models.Image;

namespace Imagino.Api.Repositories.Image
{
    public interface IImageModelProviderRepository
    {
        Task<ImageModelProvider?> GetByIdAsync(string id);
        Task<ImageModelProvider?> GetByNameAsync(string name);
        Task<List<ImageModelProvider>> GetAsync(ImageModelProviderStatus? status = null);
        Task InsertAsync(ImageModelProvider provider);
        Task UpdateAsync(ImageModelProvider provider);
        Task DeleteAsync(string id);
    }
}
