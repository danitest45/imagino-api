using System;
using Imagino.Api.Models;

namespace Imagino.Api.DTOs
{
    public record UserDto(
        string Id,
        string? Email,
        string? GoogleId,
        string? ProfileImageUrl,
        string Username,
        string? PhoneNumber,
        SubscriptionType Subscription,
        int Credits,
        DateTime CreatedAt,
        DateTime UpdatedAt);
}

