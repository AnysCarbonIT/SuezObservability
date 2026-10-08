using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Producer.Services;
using Shared.Observability;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole();

builder.Services.AddSuezObservability(
    Telemetry.ProducerServiceName,
    Telemetry.ProducerServiceName,
    Telemetry.ProducerServiceName);

builder.Services.AddTransient<RabbitMqProducer>();

using var host = builder.Build();

await host.StartAsync();

var producer =
    host.Services.GetRequiredService<RabbitMqProducer>();

await producer.SendAsync();

await host.StopAsync();