using Imagino.Api.Models;

namespace Imagino.Api.Repository
{
    public interface IEmailTokenRepository
    {
        Task CreateAsync(string userId, string purpose, string rawToken, TimeSpan ttl, string? ip = null);
        Task<EmailToken?> GetActiveByRawTokenAsync(string purpose, string rawToken);
        Task ConsumeAsync(string id);
        Task<int> CountByUserInWindowAsync(string userId, string purpose, TimeSpan window);
        Task InvalidateByUserAsync(string userId, string purpose);
    }
}
