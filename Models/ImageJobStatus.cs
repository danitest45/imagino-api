namespace Imagino.Api.Models
{
    public enum ImageJobStatus
    {
        Created,
        Queued,
        Running,
        Completed,
        Failed,

        //legacy
        Starting,
        Processing
    }
}
