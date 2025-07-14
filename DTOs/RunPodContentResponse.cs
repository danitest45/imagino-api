namespace Imagino.Api.DTOs
{
    /// <summary>
    /// Payload sent by RunPod when a job is completed.
    /// </summary>
    public class RunPodContentResponse
    {
        /// <summary>The unique ID of the RunPod job.</summary>
        public string id { get; set; }

        /// <summary>The current status of the job (e.g., COMPLETED, IN_PROGRESS).</summary>
        public string status { get; set; }

        /// <summary>The delay time before the job started, in milliseconds.</summary>
        public int delayTime { get; set; }

        /// <summary>The time it took to execute the job, in milliseconds.</summary>
        public int executionTime { get; set; }

        /// <summary>The output of the job, including image(s) and metadata.</summary>
        public Output output { get; set; }
    }

    /// <summary>
    /// Contains the generated image(s) and additional job information.
    /// </summary>
    public class Output
    {
        /// <summary>List of generated images in Base64 format.</summary>
        public List<string> images { get; set; } = new();

        /// <summary>Technical metadata about the generation process (usually in JSON format).</summary>
        public string info { get; set; } = string.Empty;
    }
}
