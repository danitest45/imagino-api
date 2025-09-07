using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using Imagino.Api.Repository;
using Imagino.Api.Settings;

namespace Imagino.Api.Services.Billing
{
    public class BillingService : IBillingService
    {
        private readonly IUserRepository _users;
        private readonly StripeSettings _settings;
        private readonly ILogger<BillingService> _logger;

        public BillingService(IUserRepository users, IOptions<StripeSettings> settings, ILogger<BillingService> logger)
        {
            _users = users;
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<string> CreateCheckoutSessionAsync(string userId, string plan)
        {
            var user = await _users.GetByIdAsync(userId) ?? throw new Exception("User not found");

            var customerId = user.StripeCustomerId;
            if (string.IsNullOrEmpty(customerId))
            {
                var customerService = new CustomerService();
                var customer = await customerService.CreateAsync(new CustomerCreateOptions
                {
                    Email = user.Email,
                });
                customerId = customer.Id;
                user.StripeCustomerId = customerId;
                await _users.UpdateAsync(user);
            }

            var price = plan.ToUpper() switch
            {
                "PRO" => _settings.PricePro,
                "ULTRA" => _settings.PriceUltra,
                _ => throw new ArgumentException("Invalid plan"),
            };

            var options = new SessionCreateOptions
            {
                Mode = "subscription",
                Customer = customerId,
                SuccessUrl = $"{_settings.SuccessUrl}?session_id={{CHECKOUT_SESSION_ID}}",
                CancelUrl = _settings.CancelUrl,
                ClientReferenceId = userId,
                Metadata = new Dictionary<string, string> { { "plan", plan.ToUpper() } },
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions { Price = price, Quantity = 1 }
                }
            };

            var service = new SessionService();
            var session = await service.CreateAsync(options);
            return session.Url;
        }

        public async Task<string> CreateCustomerPortalSessionAsync(string userId)
        {
            var user = await _users.GetByIdAsync(userId) ?? throw new Exception("User not found");
            if (string.IsNullOrEmpty(user.StripeCustomerId))
                throw new Exception("Stripe customer not found");
            var options = new Stripe.BillingPortal.SessionCreateOptions
            {
                Customer = user.StripeCustomerId,
                ReturnUrl = _settings.PortalReturnUrl,
            };
            var portalService = new Stripe.BillingPortal.SessionService();
            var session = await portalService.CreateAsync(options);
            return session.Url;
        }
    }
}
