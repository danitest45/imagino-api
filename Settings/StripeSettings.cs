namespace Imagino.Api.Settings
{
    public class StripeSettings
    {
        public string ApiKey { get; set; } = default!;
        public string WebhookSecret { get; set; } = default!;
        public string PricePro { get; set; } = default!;
        public string PriceUltra { get; set; } = default!;
        public string SuccessUrl { get; set; } = default!;
        public string CancelUrl { get; set; } = default!;
        public string PortalReturnUrl { get; set; } = default!;
    }
}
