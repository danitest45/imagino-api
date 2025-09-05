using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Imagino.Api.Services.Billing;
using Imagino.Api.Repository;

namespace Imagino.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BillingController : ControllerBase
    {
        private readonly IBillingService _billing;
        private readonly IUserRepository _users;
        private readonly ILogger<BillingController> _logger;

        public BillingController(IBillingService billing, IUserRepository users, ILogger<BillingController> logger)
        {
            _billing = billing;
            _users = users;
            _logger = logger;
        }

        public record CheckoutRequest(string Plan);
        public record UrlResponse(string Url);

        [HttpPost("checkout")]
        public async Task<IActionResult> CreateCheckoutSession([FromBody] CheckoutRequest request)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
                var url = await _billing.CreateCheckoutSessionAsync(userId, request.Plan);
                return Ok(new UrlResponse(url));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating checkout session");
                return StatusCode(500, new { message = "Error creating checkout session" });
            }
        }

        [HttpPost("portal")]
        public async Task<IActionResult> CreatePortalSession()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
                var url = await _billing.CreateCustomerPortalSessionAsync(userId);
                return Ok(new UrlResponse(url));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating portal session");
                return StatusCode(500, new { message = "Error creating portal session" });
            }
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetSubscription()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
                var user = await _users.GetByIdAsync(userId);
                if (user == null) return NotFound();
                var result = new
                {
                    user.Plan,
                    user.SubscriptionStatus,
                    user.CurrentPeriodEnd
                };
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving subscription info");
                return StatusCode(500, new { message = "Error retrieving subscription info" });
            }
        }
    }
}
