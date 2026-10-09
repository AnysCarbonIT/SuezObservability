using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace CacheApi.Observability;

public static class Telemetry
{
    public const string ServiceName = "SuezObservability.CacheApi";
    public static readonly ActivitySource ActivitySource = new(ServiceName);
    public static readonly Meter Meter = new(ServiceName);

    // Un résultat par écriture Redis : success ou failed.
    public static readonly Counter<long> CacheOperations = Meter.CreateCounter<long>("cache.operations");
}
