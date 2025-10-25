using System.Collections.Generic;

namespace Imagino.Api.Settings
{
    public class ReplicateSettings
    {
        public string? ApiKey { get; set; }
        public string? WebhookUrl { get; set; }
        public List<ReplicateModel> Models { get; set; } = new();
    }
}
