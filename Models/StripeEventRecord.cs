using System;

namespace Imagino.Api.Models
{
    public class StripeEventRecord
    {
        public string Id { get; set; } = default!;
        public string EventId { get; set; } = default!;
        public DateTime Created { get; set; }
    }
}
