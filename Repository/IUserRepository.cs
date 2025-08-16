using Imagino.Api.Models;

namespace Imagino.Api.Repository
{
    public interface IUserRepository
    {
        Task<User> GetByEmailAsync(string email);
        Task<User> GetByGoogleIdAsync(string googleId);
        Task<User?> GetByUsernameAsync(string username);
        Task<User?> GetByIdAsync(string id);
        Task<IEnumerable<User>> GetAllAsync();
        Task CreateAsync(User user);
        Task UpdateAsync(User user);
        Task DeleteAsync(string id);
        Task<bool> DecrementCreditsAsync(string userId, int amount);
        Task<bool> IncrementCreditsAsync(string userId, int amount);
        Task<int?> GetCreditsAsync(string userId);
    }
}
