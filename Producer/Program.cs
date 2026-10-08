using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Producer.Services;
using Shared.Observability;

var services = new ServiceCollection();

services.AddLogging(logging =>
{
    logging.AddConsole();
});

services.AddSuezObservability(
    Telemetry.ProducerServiceName,
    Telemetry.ProducerServiceName,
    Telemetry.ProducerServiceName);

services.AddTransient<RabbitMqProducer>();

using var serviceProvider = services.BuildServiceProvider();
serviceProvider.GetRequiredService<TracerProvider>();
serviceProvider.GetRequiredService<MeterProvider>();

var producer =serviceProvider.GetRequiredService<RabbitMqProducer>();


await producer.SendAsync();