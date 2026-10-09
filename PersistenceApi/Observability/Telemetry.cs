using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace PersistenceApi.Observability;

public static class Telemetry
{
    public const string ServiceName = "SuezObservability.PersistenceApi";
    public static readonly ActivitySource ActivitySource = new(ServiceName);
    public static readonly Meter Meter = new(ServiceName);

    // Un résultat par message : processed ou failed.
    public static readonly Counter<long> Messages = Meter.CreateCounter<long>("messages");

    // Durée du traitement, en secondes.
    public static readonly Histogram<double> MessageDuration = Meter.CreateHistogram<double>("message.duration", "s");
}
