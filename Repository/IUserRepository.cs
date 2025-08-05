using Imagino.Api.Models;

namespace Imagino.Api.Repository
{
    public interface IUserRepository
    {
        Task<User> GetByEmailAsync(string email);
        Task<User> GetByGoogleIdAsync(string googleId);
        Task CreateAsync(User user);
    }
}
