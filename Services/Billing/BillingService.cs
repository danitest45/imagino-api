using Imagino.Api.Repository;
using Imagino.Api.Settings;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
namespace Imagino.Api.Services.Billing;
public class BillingService(IUserRepository users, IOptions<StripeSettings> settings, IStripeBillingGateway stripe) : IBillingService
{
    public async Task<string> CreateCheckoutSessionAsync(string userId, string plan)
    {
        if (plan is not ("PRO" or "ULTRA")) throw new BillingRequestException(400, "Choose PRO or ULTRA");
        var user = await users.GetByIdAsync(userId) ?? throw new BillingRequestException(404, "User not found");
        if (string.IsNullOrWhiteSpace(settings.Value.ApiKey)) throw new BillingRequestException(503, "Billing is disabled");
        if (!string.IsNullOrEmpty(user.StripeSubscriptionId) && user.SubscriptionStatus is not ("canceled" or "incomplete_expired"))
            throw new BillingRequestException(409, "An existing subscription must be managed in the billing portal");
        var customerId = user.StripeCustomerId;
        if (string.IsNullOrEmpty(customerId)) {
            var customer = await stripe.CreateCustomerAsync(new CustomerCreateOptions { Email = user.Email },
                new RequestOptions { IdempotencyKey = $"imagino-customer-{userId}" });
            customerId = customer.Id;
            await users.SetStripeCustomerIdAsync(userId, customerId);
        }
        // Concurrent attempts share this key, including conflicting plan choices.
        var session = await stripe.CreateCheckoutAsync(new SessionCreateOptions {
            Mode = "subscription", Customer = customerId,
            SuccessUrl = settings.Value.SuccessUrl, CancelUrl = settings.Value.CancelUrl,
            ClientReferenceId = userId,
            LineItems = [new SessionLineItemOptions { Price = plan == "PRO" ? settings.Value.PricePro : settings.Value.PriceUltra, Quantity = 1 }]
        }, new RequestOptions { IdempotencyKey = $"imagino-checkout-{userId}-{user.BillingRevision}" });
        return session.Url;
    }
    public async Task<string> CreateCustomerPortalSessionAsync(string userId)
    {
        var user = await users.GetByIdAsync(userId) ?? throw new BillingRequestException(404, "User not found");
        if (string.IsNullOrEmpty(user.StripeCustomerId)) throw new BillingRequestException(409, "No billing customer");
        var session = await stripe.CreatePortalAsync(new Stripe.BillingPortal.SessionCreateOptions {
            Customer = user.StripeCustomerId, ReturnUrl = settings.Value.PortalReturnUrl
        });
        return session.Url;
    }
}
