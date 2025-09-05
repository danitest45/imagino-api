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
            try
            {
                if (!Request.Headers.TryGetValue("Stripe-Signature", out var signature) || string.IsNullOrEmpty(signature))
                    return Unauthorized();

                var stripeEvent = EventUtility.ConstructEvent(json, signature, _settings.WebhookSecret);

                if (await _events.ExistsAsync(stripeEvent.Id))
                    return Ok();

                switch (stripeEvent.Type)
                {
                    case "checkout.session.completed":
                        await HandleCheckoutSessionCompleted(stripeEvent);
                        break;
                    case "customer.subscription.updated":
                    case "customer.subscription.deleted":
                        await HandleSubscriptionUpdated(stripeEvent);
                        break;
                    default:
                        break;
                }

                await _events.CreateAsync(new Models.StripeEventRecord { EventId = stripeEvent.Id, Created = stripeEvent.Created });

                return Ok();
            }
            catch (StripeException e)
            {
                _logger.LogError(e, "Stripe webhook error");
                return BadRequest();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled webhook error");
                return BadRequest();
            }
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
                }

                await _users.UpdateAsync(user);
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
    }
}
