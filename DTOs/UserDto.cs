using System;
using Imagino.Api.Models;

namespace Imagino.Api.DTOs
{
    public record UserDto(
        string Id,
        string? Email,
        string? Username,
        string? PhoneNumber,
        SubscriptionPlan Subscription,
        int Credits,
        string? GoogleId,
        string? ProfileImageUrl,
        DateTime CreatedAt,
        DateTime UpdatedAt);
}

