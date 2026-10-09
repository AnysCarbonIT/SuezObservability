using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Producer.Observability;
using Producer.Services;
using Shared.Messaging;
using Shared.Observability;

// Le host charge appsettings et démarre les services, dont OpenTelemetry.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});
builder.Services.AddSuezObservability(Telemetry.ServiceName, Telemetry.ServiceName, Telemetry.ServiceName);
builder.Services.AddRabbitMq(builder.Configuration, Telemetry.ServiceName);
// Le host démarre et ferme la connexion singleton injectée dans le Producer.
builder.Services.AddSingleton<RabbitMqConnection>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<RabbitMqConnection>());
builder.Services.AddSingleton<MessageFactory>();
builder.Services.AddSingleton<RabbitMqProducer>();

using var host = builder.Build();
await host.StartAsync();
try
{
    var producer = host.Services.GetRequiredService<RabbitMqProducer>();
    await producer.SendAsync(host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping);
}
finally
{
    await host.StopAsync();
}
