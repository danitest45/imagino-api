namespace Imagino.Api.Settings
{
    public class RefreshTokenCookieSettings
    {
        public bool Secure { get; set; }
        public string SameSite { get; set; } = string.Empty;
        public string? Domain { get; set; }
        public bool HttpOnly { get; set; }
        public int ExpiresDays { get; set; }
    }
}
