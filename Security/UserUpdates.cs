using Imagino.Api.Models;
using MongoDB.Driver;

namespace Imagino.Api.Security;

public static class UserUpdates
{
    public static UpdateDefinition<User> VerifyEmail(DateTime now) => Builders<User>.Update
        .Set(u => u.EmailVerified, true).Set(u => u.VerifiedAt, now).Set(u => u.UpdatedAt, now);
    public static UpdateDefinition<User> Password(string hash) => Builders<User>.Update
        .Set(u => u.PasswordHash, hash).Set(u => u.UpdatedAt, DateTime.UtcNow);
    public static UpdateDefinition<User> Customer(string id) => Builders<User>.Update
        .Set(u => u.StripeCustomerId, id).Set(u => u.UpdatedAt, DateTime.UtcNow);
    public static UpdateDefinition<User> Billing(User user) => Builders<User>.Update
        .Set(u => u.StripeCustomerId, user.StripeCustomerId)
        .Set(u => u.StripeSubscriptionId, user.StripeSubscriptionId)
        .Set(u => u.Plan, user.Plan).Set(u => u.SubscriptionStatus, user.SubscriptionStatus)
        .Set(u => u.CurrentPeriodEnd, user.CurrentPeriodEnd).Set(u => u.UpdatedAt, DateTime.UtcNow);
}
