namespace Imagino.Api.DTOs
{
    public class RunPodContentResponse
    {
        public string id { get; set; }
        public string status { get; set; }
        public int delayTime { get; set; }
        public int executionTime { get; set; }
        public Output output { get; set; }
    }

    public class Output
    {
        public List<string> images { get; set; } = new();
        public string info { get; set; } = string.Empty;
    }
}
