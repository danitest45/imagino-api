using Stripe;
using Imagino.Api.Settings;
using Microsoft.Extensions.Options;
namespace Imagino.Api.Services.Billing;
// Each integration owns its client; there is no global Stripe key.
public sealed class StripeBillingGateway(IOptions<StripeSettings> settings) : IStripeBillingGateway
{
    private StripeClient Client => !string.IsNullOrWhiteSpace(settings.Value.ApiKey)
        ? new StripeClient(settings.Value.ApiKey) : throw new BillingRequestException(503, "Billing is disabled");
    public Task<Customer> CreateCustomerAsync(CustomerCreateOptions options, RequestOptions request) =>
        new CustomerService(Client).CreateAsync(options, request);
    public Task<Stripe.Checkout.Session> CreateCheckoutAsync(Stripe.Checkout.SessionCreateOptions options, RequestOptions request) =>
        new Stripe.Checkout.SessionService(Client).CreateAsync(options, request);
    public Task<Stripe.BillingPortal.Session> CreatePortalAsync(Stripe.BillingPortal.SessionCreateOptions options) =>
        new Stripe.BillingPortal.SessionService(Client).CreateAsync(options);
    public Task<Subscription> GetSubscriptionAsync(string id) => new SubscriptionService(Client).GetAsync(id);
    public Task<Invoice> GetInvoiceAsync(string id) => new InvoiceService(Client).GetAsync(id);
}
