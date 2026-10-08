using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry.Logs;
using OpenTelemetry;


namespace Shared.Observability
{
    public static class OpenTelemetryExtensions
    {
        public static IServiceCollection AddSuezObservability(
            this IServiceCollection services,
            string serviceName,
            string activitySourceName,
            string meterName, Action<TracerProviderBuilder>? configureTracing = null)
        {
            services
                .AddOpenTelemetry() // active l'OpenTelemetry pour la collecte de traces
                .ConfigureResource(resource =>resource.AddService(serviceName)) //indique le nom du service pour lequel on collecte les traces
                .WithLogging(configureBuilder: null, configureOptions: options =>
                {
                    options.IncludeFormattedMessage = true;
                    options.IncludeScopes = true;
                })
                .WithTracing(tracing =>
                {
                    // Écoute les Activity créées par le service.
                    tracing.AddSource(activitySourceName);
                    // Permet à chaque application d'ajouter ses instrumentations spécifiques.
                    configureTracing?.Invoke(tracing);
                })
                 .WithMetrics(metrics =>
                 {
                     metrics
                         .AddMeter(meterName)
                         .AddRuntimeInstrumentation(); // collecte des métriques sur l'exécution de l'application .Net
                 })
                 .UseOtlpExporter();
           
            
            return services;
        }
    }
}