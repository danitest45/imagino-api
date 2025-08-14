using System;
using Imagino.Api.Models;

namespace Imagino.Api.DTOs
{
    public class CreateUserDto
    {
        public string Email { get; set; } = default!;
        public string Password { get; set; } = default!;
        public string Username { get; set; } = default!;
        public string? PhoneNumber { get; set; }
        public SubscriptionType Subscription { get; set; } = SubscriptionType.Free;
        public int Credits { get; set; } = 0;
    }
}

