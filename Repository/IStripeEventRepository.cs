using System.Threading.Tasks;
using Imagino.Api.Models;

namespace Imagino.Api.Repository
{
    public interface IStripeEventRepository
    {
        Task<bool> ExistsAsync(string eventId);
        Task CreateAsync(StripeEventRecord record);
    }
}
