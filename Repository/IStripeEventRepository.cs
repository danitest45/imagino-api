using Imagino.Api.Models;
namespace Imagino.Api.Repository;
public enum StripeClaim { Acquired, Completed, Busy }
public interface IStripeEventRepository
{
    Task<StripeClaim> TryClaimAsync(StripeEventRecord record);
    Task CompleteAsync(string eventId, string claimId);
    Task FailAsync(string eventId, string claimId);
}
