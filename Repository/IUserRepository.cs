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
        Task MarkEmailVerifiedAsync(string id, DateTime now);
        Task SetPasswordHashAsync(string id, string hash);
        Task SetStripeCustomerIdAsync(string id, string customerId);
        Task UpdateBillingAsync(User user);
        Task<bool> UpdateBillingSnapshotAsync(User user, DateTime created);
        Task UpdateProfileAsync(string id, string username, string? phoneNumber, DateTime updatedAt);
        Task UpdateProfileImageAsync(string id, string imageUrl, DateTime updatedAt);
        Task DeleteAsync(string id);
        Task<bool> DecrementCreditsAsync(string userId, int amount);
        Task<bool> IncrementCreditsAsync(string userId, int amount);
        Task<bool> IncrementBillingCreditsOnceAsync(string userId, int amount, string eventId);
        Task<int?> GetCreditsAsync(string userId);
        Task<User?> GetByStripeCustomerIdAsync(string customerId);
    }
}
