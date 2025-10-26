using Imagino.Api.Models.Image;

namespace Imagino.Api.Repositories.Image
{
    public interface IImageModelPresetRepository
    {
        Task<ImageModelPreset?> GetByIdAsync(string id);
        Task<ImageModelPreset?> GetBySlugAsync(string modelId, string slug);
        Task<List<ImageModelPreset>> GetByModelIdAsync(string modelId, ImageModelPresetStatus? status = null, ImageModelVisibility? visibility = null);
        Task InsertAsync(ImageModelPreset preset);
        Task UpdateAsync(ImageModelPreset preset);
        Task DeleteAsync(string id);
    }
}
