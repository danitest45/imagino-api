using System.Threading.Tasks;

namespace Imagino.Api.Services.Billing
{
    public interface IBillingService
    {
        Task<string> CreateCheckoutSessionAsync(string userId, string plan);
        Task<string> CreateCustomerPortalSessionAsync(string userId);
    }
}
