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

        // Nombre de messages complètement traités avec succès par PersistenceApi.
        public static readonly Counter<long> ProcessedMessages =PersistenceApiMeter.CreateCounter<long>("messages.processed");

        // Nombre de messages dont le traitement a échoué.
        public static readonly Counter<long> FailedMessages =PersistenceApiMeter.CreateCounter<long>("messages.failed");
        // Nombre d'écritures réussies dans Redis.
        public static readonly Counter<long> CacheWrites =CacheApiMeter.CreateCounter<long>("cache.writes");
        // Nombre d'échecs lors des écritures dans Redis.
        public static readonly Counter<long> CacheFailures =CacheApiMeter.CreateCounter<long>("cache.failures");
    }
}
