using System.Collections.Generic;

namespace Imagino.Api.Settings
{
    public class ReplicateSettings
    {
        public string? ApiKey { get; set; }
        public string? WebhookUrl { get; set; }
        public Dictionary<string, string> Models { get; set; } = new();
    }
}
