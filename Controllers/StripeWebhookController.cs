using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Stripe;
using Imagino.Api.Repository;
using Imagino.Api.Settings;
using Imagino.Api.Models;
using Imagino.Api.Services.Billing;
namespace Imagino.Api.Controllers;
[ApiController, Route("api/stripe/webhook")]
public class StripeWebhookController(IUserRepository users, IStripeEventRepository events,
    IOptions<StripeSettings> options, IOptions<ImageGeneratorSettings> database,
    IStripeBillingGateway stripe, ILogger<StripeWebhookController> logger) : ControllerBase
{
    private StripeSettings Settings => options.Value;
    private bool TestOnly => database.Value.MongoDatabase == "imagino_staging";
    [HttpPost, Microsoft.AspNetCore.Authorization.AllowAnonymous, RequestSizeLimit(1024 * 1024)]
    public async Task<IActionResult> Handle()
    {
        if (string.IsNullOrWhiteSpace(Settings.WebhookSecret)) return StatusCode(503);
        if (!Request.Headers.TryGetValue("Stripe-Signature", out var signature) || string.IsNullOrWhiteSpace(signature))
            return BadRequest(new { message = "Invalid webhook signature" });
        Event ev;
        try {
            var json = await new StreamReader(Request.Body).ReadToEndAsync();
            ev = EventUtility.ConstructEvent(json, signature, Settings.WebhookSecret);
        } catch (Exception ex) when (ex is StripeException or Newtonsoft.Json.JsonException or ArgumentException) {
            return BadRequest(new { message = "Invalid webhook signature or event" });
        }
        if (string.IsNullOrWhiteSpace(ev.Id) || (TestOnly && ev.Livemode)) return BadRequest();
        var claimId = Guid.NewGuid().ToString("N");
        var acquired = false;
        try {
            var claim = await events.TryClaimAsync(new StripeEventRecord {
                EventId = ev.Id, Created = ev.Created, Type = ev.Type, ClaimId = claimId
            });
            if (claim == StripeClaim.Completed) return Ok();
            if (claim == StripeClaim.Busy) return StatusCode(503);
            acquired = true;
            switch (ev.Type) {
                case "checkout.session.completed":
                    if (ev.Data.Object is Stripe.Checkout.Session session && session.Mode == "subscription" &&
                        session.PaymentStatus == "paid" && !string.IsNullOrEmpty(session.SubscriptionId) &&
                        !string.IsNullOrEmpty(session.ClientReferenceId)) {
                        var user = await users.GetByIdAsync(session.ClientReferenceId);
                        if (user != null && !string.IsNullOrEmpty(user.StripeCustomerId) && user.StripeCustomerId == session.CustomerId) {
                            var sub = await Reconcile(user, session.SubscriptionId, ev.Created);
                            var invoiceId = session.InvoiceId ?? sub?.LatestInvoiceId;
                            if (!string.IsNullOrEmpty(invoiceId)) await CreditInvoice(await stripe.GetInvoiceAsync(invoiceId));
                        }
                    }
                    break;
                case "invoice.paid":
                case "invoice.payment_succeeded":
                    if (ev.Data.Object is Invoice invoice) {
                        await CreditInvoice(invoice);
                        var user = await users.GetByStripeCustomerIdAsync(invoice.CustomerId);
                        var id = invoice.Parent?.SubscriptionDetails?.SubscriptionId;
                        if (user != null && !string.IsNullOrEmpty(id)) await Reconcile(user, id, ev.Created);
                    }
                    break;
                case "customer.subscription.updated":
                case "customer.subscription.deleted":
                    if (ev.Data.Object is Stripe.Subscription subscription) {
                        var user = await users.GetByStripeCustomerIdAsync(subscription.CustomerId);
                        if (user != null) await Reconcile(user, subscription.Id, ev.Created);
                    }
                    break;
            }
            await events.CompleteAsync(ev.Id, claimId);
            logger.LogInformation("Stripe webhook processing completed. Type={Type}", ev.Type);
            return Ok();
        } catch (Exception) {
            if (acquired) { try { await events.FailAsync(ev.Id, claimId); } catch (Exception) { } }
            logger.LogWarning("Stripe webhook processing failed; retry required");
            return StatusCode(503);
        }
    }
    private async Task<Stripe.Subscription?> Reconcile(User user, string subscriptionId, DateTime created)
    {
        for (var attempt = 0; attempt < 3; attempt++) {
            if (user.LastSubscriptionEventAt > created) return null;
            // Fresh Stripe state handles equal-second events; CAS prevents stale reads overwriting concurrent updates.
            var sub = await stripe.GetSubscriptionAsync(subscriptionId);
            if (sub.CustomerId != user.StripeCustomerId || (TestOnly && sub.Livemode)) return null;
            if (sub.Items?.Data?.Count != 1) return null;
            var item = sub.Items.Data[0];
            var plan = Plan(item.Price?.Id);
            if (plan == null || item.Quantity != 1) return null;
            user.StripeSubscriptionId = sub.Id;
            user.SubscriptionStatus = sub.Status;
            user.Plan = sub.Status is "canceled" or "incomplete_expired" ? null : plan;
            user.Subscription = sub.Status is "active" or "trialing"
                ? plan == "PRO" ? SubscriptionType.Premium : SubscriptionType.Ultra : SubscriptionType.Free;
            user.CurrentPeriodEnd = new DateTimeOffset(DateTime.SpecifyKind(item.CurrentPeriodEnd, DateTimeKind.Utc));
            if (await users.UpdateBillingSnapshotAsync(user, created)) return sub;
            user = await users.GetByIdAsync(user.Id!) ?? throw new InvalidOperationException("Billing user missing");
        }
        throw new InvalidOperationException("Billing reconciliation contention");
    }
    private async Task CreditInvoice(Invoice invoice)
    {
        if (invoice.Status != "paid" || invoice.AmountPaid <= 0 || (TestOnly && invoice.Livemode) ||
            invoice.BillingReason is not ("subscription_create" or "subscription_cycle") ||
            string.IsNullOrEmpty(invoice.Id) || string.IsNullOrEmpty(invoice.CustomerId) ||
            string.IsNullOrEmpty(invoice.Parent?.SubscriptionDetails?.SubscriptionId)) return;
        var lines = invoice.Lines;
        if (lines?.HasMore != false || lines.Data.Count != 1) return;
        var line = lines.Data[0];
        if (line.Quantity != 1 || line.Parent?.SubscriptionItemDetails?.Proration != false) return;
        var plan = Plan(line.Pricing?.PriceDetails?.PriceId);
        if (plan == null) return;
        var user = await users.GetByStripeCustomerIdAsync(invoice.CustomerId);
        if (user?.Id == null) return;
        await users.IncrementBillingCreditsOnceAsync(user.Id, plan == "PRO" ? Settings.CreditsPro : Settings.CreditsUltra,
            $"invoice-credit-{invoice.Id}");
    }
    private string? Plan(string? price) => !string.IsNullOrEmpty(price) && price == Settings.PricePro ? "PRO" :
        !string.IsNullOrEmpty(price) && price == Settings.PriceUltra ? "ULTRA" : null;
}
