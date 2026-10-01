using Stripe;
namespace Imagino.Api.Services.Billing;
public interface IStripeBillingGateway
{
    Task<Customer> CreateCustomerAsync(CustomerCreateOptions options, RequestOptions request);
    Task<Stripe.Checkout.Session> CreateCheckoutAsync(Stripe.Checkout.SessionCreateOptions options, RequestOptions request);
    Task<Stripe.BillingPortal.Session> CreatePortalAsync(Stripe.BillingPortal.SessionCreateOptions options);
    Task<Subscription> GetSubscriptionAsync(string id);
    Task<Invoice> GetInvoiceAsync(string id);
}
