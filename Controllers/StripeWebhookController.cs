using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using Imagino.Api.Repository;
using Imagino.Api.Errors;
using Imagino.Api.Settings;
using Newtonsoft.Json.Linq;

namespace Imagino.Api.Controllers
{
    [ApiController]
    [Route("api/stripe/webhook")]
    public class StripeWebhookController : ControllerBase
    {
        private readonly IUserRepository _users;
        private readonly IStripeEventRepository _events;
        private readonly StripeSettings _settings;
        private readonly ILogger<StripeWebhookController> _logger;

        public StripeWebhookController(IUserRepository users, IStripeEventRepository events, IOptions<StripeSettings> settings, ILogger<StripeWebhookController> logger)
        {
            _users = users;
            _events = events;
            _settings = settings.Value;
            _logger = logger;
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Handle()
        {
            var json = await new StreamReader(Request.Body).ReadToEndAsync();

            if (!Request.Headers.TryGetValue("Stripe-Signature", out var signature) || string.IsNullOrEmpty(signature))
                throw new WebhookSignatureException();

            Event stripeEvent;
            try
            {
                stripeEvent = EventUtility.ConstructEvent(json, signature, _settings.WebhookSecret);
            }
            catch (StripeException e)
            {
                throw new WebhookSignatureException(e.Message);
            }

            if (await _events.ExistsAsync(stripeEvent.Id))
                return Ok();

            try
            {
                switch (stripeEvent.Type)
                {
                    case "checkout.session.completed":
                        await HandleCheckoutSessionCompleted(stripeEvent);
                        break;
                    case "invoice.paid":
                    case "invoice.payment_succeeded":
                        await HandleInvoicePaid(stripeEvent);
                        break;
                    case "customer.subscription.updated":
                    case "customer.subscription.deleted":
                        await HandleSubscriptionUpdated(stripeEvent);
                        break;
                    default:
                        break;
                }

                await _events.CreateAsync(new Models.StripeEventRecord { EventId = stripeEvent.Id, Created = stripeEvent.Created });
            }
            catch (StripeException e)
            {
                _logger.LogError(e, "Stripe webhook error");
                throw new StripeServiceException(e.Message, e.StripeError?.Code);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled webhook error");
                throw new WebhookProcessingException();
            }

            return Ok();
        }

        private async Task HandleCheckoutSessionCompleted(Event stripeEvent)
        {
            if (stripeEvent.Data.Object is Session session)
            {
                var userId = session.ClientReferenceId;
                if (string.IsNullOrEmpty(userId)) return;
                var user = await _users.GetByIdAsync(userId);
                if (user == null) return;

                user.StripeCustomerId = session.CustomerId;
                user.StripeSubscriptionId = session.SubscriptionId;
                user.Plan = session.Metadata != null && session.Metadata.TryGetValue("plan", out var plan)
                    ? plan
                    : user.Plan;

                if (!string.IsNullOrEmpty(session.SubscriptionId))
                {
                    var subService = new SubscriptionService();
                    var sub = await subService.GetAsync(session.SubscriptionId);
                    user.SubscriptionStatus = sub.Status;
                    var periodEndUnix = sub.RawJObject?["current_period_end"]?.Value<long?>();
                    if (periodEndUnix.HasValue)
                        user.CurrentPeriodEnd = DateTimeOffset.FromUnixTimeSeconds(periodEndUnix.Value);

                    var priceId = sub.Items.Data.Count > 0 ? sub.Items.Data[0].Price.Id : null;
                    if (!string.IsNullOrEmpty(priceId))
                    {
                        user.Plan = MapPlanFromPrice(priceId, user.Plan);
                    }
                }

                await _users.UpdateAsync(user);

                var creditsToAdd = GetCreditsForPlan(user.Plan);
                if (creditsToAdd > 0 && string.Equals(session.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase))
                {
                    var incremented = await _users.IncrementCreditsAsync(user.Id!, creditsToAdd);
                    if (incremented)
                    {
                        _logger.LogInformation("Credits added on checkout session completed. UserId={UserId}, Plan={Plan}, Credits={Credits}, EventId={EventId}", user.Id, user.Plan, creditsToAdd, stripeEvent.Id);
                    }
                }
            }
        }

        private async Task HandleSubscriptionUpdated(Event stripeEvent)
        {
            if (stripeEvent.Data.Object is Stripe.Subscription sub)
            {
                var user = await _users.GetByStripeCustomerIdAsync(sub.CustomerId);
                if (user == null) return;

                user.StripeSubscriptionId = sub.Id;
                user.SubscriptionStatus = sub.Status;
                var periodEnd = sub.RawJObject?["current_period_end"]?.Value<long?>();
                if (periodEnd.HasValue)
                    user.CurrentPeriodEnd = DateTimeOffset.FromUnixTimeSeconds(periodEnd.Value);

                var priceId = sub.Items.Data.Count > 0 ? sub.Items.Data[0].Price.Id : null;
                if (!string.IsNullOrEmpty(priceId))
                {
                    user.Plan = priceId == _settings.PricePro ? "PRO" :
                                priceId == _settings.PriceUltra ? "ULTRA" : user.Plan;
                }

                await _users.UpdateAsync(user);
            }
        }

        private async Task HandleInvoicePaid(Event stripeEvent)
        {
            if (stripeEvent.Data.Object is Invoice invoice)
            {
                if (!invoice.Paid)
                {
                    return;
                }

                if (string.Equals(invoice.BillingReason, "subscription_create", StringComparison.OrdinalIgnoreCase))
                {
                    // Crédito inicial já tratado no checkout
                    return;
                }

                var customerId = invoice.CustomerId;
                var subscriptionId = invoice.SubscriptionId;

                if (string.IsNullOrEmpty(customerId)) return;

                var user = await _users.GetByStripeCustomerIdAsync(customerId);
                if (user == null) return;

                Stripe.Subscription? subscription = null;
                if (!string.IsNullOrEmpty(subscriptionId))
                {
                    var subscriptionService = new SubscriptionService();
                    subscription = await subscriptionService.GetAsync(subscriptionId);

                    user.StripeSubscriptionId = subscription.Id;
                    user.SubscriptionStatus = subscription.Status;

                    var periodEnd = subscription.RawJObject?["current_period_end"]?.Value<long?>();
                    if (periodEnd.HasValue)
                        user.CurrentPeriodEnd = DateTimeOffset.FromUnixTimeSeconds(periodEnd.Value);

                    var priceId = subscription.Items.Data.Count > 0 ? subscription.Items.Data[0].Price.Id : null;
                    if (!string.IsNullOrEmpty(priceId))
                    {
                        user.Plan = MapPlanFromPrice(priceId, user.Plan);
                    }

                    await _users.UpdateAsync(user);
                }

                var creditsToAdd = GetCreditsForPlan(user.Plan);
                if (creditsToAdd > 0)
                {
                    var creditEventId = $"invoice-credit-{invoice.Id}";
                    if (await _events.ExistsAsync(creditEventId)) return;

                    var incremented = await _users.IncrementCreditsAsync(user.Id!, creditsToAdd);
                    if (incremented)
                    {
                        await _events.CreateAsync(new Models.StripeEventRecord { EventId = creditEventId, Created = DateTime.UtcNow });
                        _logger.LogInformation("Credits added on invoice paid. UserId={UserId}, Plan={Plan}, Credits={Credits}, EventId={EventId}, InvoiceId={InvoiceId}", user.Id, user.Plan, creditsToAdd, stripeEvent.Id, invoice.Id);
                    }
                }
            }
        }

        private int GetCreditsForPlan(string? plan)
        {
            if (string.IsNullOrEmpty(plan)) return 0;

            return plan.ToUpperInvariant() switch
            {
                "PRO" => _settings.CreditsPro,
                "ULTRA" => _settings.CreditsUltra,
                _ => 0
            };
        }

        private string MapPlanFromPrice(string priceId, string? currentPlan)
        {
            if (priceId == _settings.PricePro) return "PRO";
            if (priceId == _settings.PriceUltra) return "ULTRA";
            return currentPlan ?? string.Empty;
        }
    }
}
