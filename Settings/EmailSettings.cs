namespace Imagino.Api.Settings
{
    public class EmailSettings
    {
        public string Provider { get; set; } = "Resend";
        public string From { get; set; } = string.Empty;
        public string FromName { get; set; } = string.Empty;
        public TemplateSettings Template { get; set; } = new();

        public class TemplateSettings
        {
            public string? Verify { get; set; }
            public string? Reset { get; set; }
        }
    }
}
