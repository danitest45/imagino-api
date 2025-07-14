namespace Imagino.Api.Settings
{
    public class ImageGeneratorSettings
    {
        public string RunPodApiUrl { get; set; } = string.Empty;
        public string RunPodApiKey { get; set; } = string.Empty;
        public string MongoConnection { get; set; } = string.Empty;
        public string MongoDatabase { get; set; } = string.Empty;
        public string JobsCollection { get; set; } = "image_jobs";
    }
}
