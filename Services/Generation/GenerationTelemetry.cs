using System.Diagnostics.Metrics;

namespace Imagino.Api.Services.Generation;

public static class GenerationTelemetry
{
    public static readonly Meter Meter = new("Imagino.Generation", "1.0");
    public static readonly Counter<long> Requests = Meter.CreateCounter<long>("imagino.requests");
    public static readonly Histogram<double> RequestSeconds = Meter.CreateHistogram<double>("imagino.request_seconds", "s");
    public static readonly Counter<long> Attempts = Meter.CreateCounter<long>("imagino.generation.attempts");
    public static readonly Counter<long> Settlements = Meter.CreateCounter<long>("imagino.generation.settlements");
    public static readonly Histogram<double> QueueSeconds = Meter.CreateHistogram<double>("imagino.generation.queue_seconds", "s");
    public static readonly Histogram<double> DurationSeconds = Meter.CreateHistogram<double>("imagino.generation.duration_seconds", "s");
    public static readonly Histogram<double> StorageSeconds = Meter.CreateHistogram<double>("imagino.generation.storage_seconds", "s");
    public static readonly Counter<double> EstimatedUsd = Meter.CreateCounter<double>("imagino.generation.provider_cost_estimate_usd", "USD");
    public static readonly Counter<long> Credits = Meter.CreateCounter<long>("imagino.generation.charged_credits");
    public static readonly Counter<long> Unknown = Meter.CreateCounter<long>("imagino.generation.submission_unknown");
    public static KeyValuePair<string, object?>[] Labels(GenerationJob job) => new[] {
        new KeyValuePair<string, object?>("provider", job.Model.Provider), new("model", job.Model.Id)
    };
}
