using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;

namespace Shared.Observability
{
    public static class Telemetry
    {
        public const string ProducerServiceName = "SuezObservability.Producer";
        public const string PersistenceApiServiceName = "SuezObservability.PersistenceApi";
        //public const string CacheApiServiceName = "SuezObservability.CacheApi";



        public static readonly ActivitySource ProducerActivitySource = new ActivitySource(ProducerServiceName); // pour les creation de spans Producer
        public static readonly ActivitySource PersistenceApiActivitySource = new ActivitySource(PersistenceApiServiceName); // pour les creation de spans PersistenceApi
        // public static readonly ActivitySource CacheApiActivitySource = new ActivitySource(CacheApiServiceName); // pour les creation de spans CaheApi

        public static readonly Meter ProducerMeter = new Meter(ProducerServiceName);

        public static readonly Meter PersistenceApiMeter = new Meter(PersistenceApiServiceName);
    }
}
