using Imagino.Api.Models;

namespace Imagino.Api.DTOs
{
    public class UpdateUserDto
    {
        public string? Email { get; set; }
        public string? Username { get; set; }
        public string? PhoneNumber { get; set; }
        public SubscriptionPlan? Subscription { get; set; }
        public int? Credits { get; set; }
        public string? Password { get; set; }
        public string? ProfileImageUrl { get; set; }
    }
}
