using System.Collections.Generic;
using Imagino.Api.DTOs;
using Imagino.Api.Models.Image;

namespace Imagino.Api.Repositories.Image
{
    public interface IImageModelRepository
    {
        Task<ImageModel?> GetByIdAsync(string id);
        Task<ImageModel?> GetBySlugAsync(string slug);
        Task<List<ImageModel>> GetAsync(ImageModelStatus? status = null, ImageModelVisibility? visibility = null, IEnumerable<string>? ids = null);
        Task<PagedResult<ImageModel>> GetPagedAsync(ImageModelStatus? status, ImageModelVisibility? visibility, int page, int pageSize);
        Task InsertAsync(ImageModel model);
        Task UpdateAsync(ImageModel model);
        Task DeleteAsync(string id);
        Task SetDefaultVersionAsync(string modelId, string versionId);
    }
}
