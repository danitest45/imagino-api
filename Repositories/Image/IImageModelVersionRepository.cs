using Imagino.Api.Models.Image;

namespace Imagino.Api.Repositories.Image
{
    public interface IImageModelVersionRepository
    {
        Task<ImageModelVersion?> GetByIdAsync(string id);
        Task<ImageModelVersion?> GetByModelAndTagAsync(string modelId, string versionTag);
        Task<List<ImageModelVersion>> GetByModelIdAsync(string modelId, ImageModelVersionStatus? status = null);
        Task InsertAsync(ImageModelVersion version);
        Task UpdateAsync(ImageModelVersion version);
        Task DeleteAsync(string id);
    }
}
