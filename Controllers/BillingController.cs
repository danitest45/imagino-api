using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Imagino.Api.Services.Billing;
using Imagino.Api.Repository;
namespace Imagino.Api.Controllers;

[ApiController, Route("api/[controller]"), Authorize]
public class BillingController(IBillingService billing, IUserRepository users, ILogger<BillingController> logger) : ControllerBase
{
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public record CheckoutRequest(string Plan);
    public record UrlResponse(string Url);
    public static string? Identity(ClaimsPrincipal user) =>
        user.FindFirstValue(JwtRegisteredClaimNames.Sub) is { Length: > 0 } sub ? sub :
        user.FindFirstValue(ClaimTypes.NameIdentifier) is { Length: > 0 } fallback ? fallback : null;
    [HttpPost("checkout")]
    public Task<IActionResult> CreateCheckoutSession(CheckoutRequest request) =>
        Execute(id => billing.CreateCheckoutSessionAsync(id, request.Plan));
    [HttpPost("portal")]
    public Task<IActionResult> CreatePortalSession() => Execute(billing.CreateCustomerPortalSessionAsync);
    private async Task<IActionResult> Execute(Func<string, Task<string>> action)
    {
        var id = Identity(User);
        if (string.IsNullOrWhiteSpace(id)) return Unauthorized();
        try { return Ok(new UrlResponse(await action(id))); }
        catch (BillingRequestException ex) { return StatusCode(ex.Status, new { message = ex.Message }); }
        catch (Exception) {
            // Provider exceptions can contain request/response data. Never log them.
            logger.LogWarning("Billing provider request failed");
            return StatusCode(503, new { message = "Billing temporarily unavailable" });
        }
    }
    [HttpGet("me")]
    public async Task<IActionResult> GetSubscription()
    {
        var id = Identity(User);
        if (string.IsNullOrWhiteSpace(id)) return Unauthorized();
        var user = await users.GetByIdAsync(id);
        if (user == null) return NotFound();
        return Ok(new { user.Plan, user.SubscriptionStatus, user.CurrentPeriodEnd, user.Credits,
            HasPaidInvoice = user.BillingCreditEvents?.Count > 0 });
    }
}
