using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Producer.Observability;

public static class Telemetry
{
    public const string ServiceName = "SuezObservability.Producer";
    public static readonly ActivitySource ActivitySource = new(ServiceName);
    public static readonly Meter Meter = new(ServiceName);
}
