using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Shared.Observability
{
    public static class Telemetry
    {
        public const string ProducerServiceName = "SuezObservability.Producer";
        public const string PersistenceApiServiceName = "SuezObservability.PersistenceApi";
        public const string CacheApiServiceName = "SuezObservability.CacheApi";



        public static readonly ActivitySource ProducerActivitySource = new ActivitySource(ProducerServiceName); // pour les creation de spans Producer
        public static readonly ActivitySource PersistenceApiActivitySource = new ActivitySource(PersistenceApiServiceName); // pour les creation de spans PersistenceApi
        public static readonly ActivitySource CacheApiActivitySource = new ActivitySource(CacheApiServiceName); // pour les creation de spans CacheApi



        public static readonly Meter ProducerMeter = new Meter(ProducerServiceName);

        public static readonly Meter PersistenceApiMeter = new Meter(PersistenceApiServiceName);

        public static readonly Meter CacheApiMeter = new Meter(CacheApiServiceName);

        // Un résultat par livraison, après les éventuels retries : processed ou failed.
        public static readonly Counter<long> Messages = PersistenceApiMeter.CreateCounter<long>("messages");

        // Durée totale du traitement, pauses entre tentatives comprises.
        public static readonly Histogram<double> MessageDuration = PersistenceApiMeter.CreateHistogram<double>("message.duration", "s");

        // Un résultat par écriture Redis : success ou failed.
        public static readonly Counter<long> CacheOperations = CacheApiMeter.CreateCounter<long>("cache.operations");
    }
}
