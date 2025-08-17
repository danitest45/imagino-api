using Imagino.Api.Models;
using System.Threading.Tasks;

namespace Imagino.Api.Repository
{
    public interface IRefreshTokenRepository
    {
        Task CreateAsync(RefreshToken token);
        Task<RefreshToken?> GetByTokenAsync(string token);
        Task DeleteAsync(string token);
        Task DeleteByUserIdAsync(string userId);
    }
}
