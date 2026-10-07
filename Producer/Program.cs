using Microsoft.Extensions.Logging;
using Producer.Services;

using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
});

ILogger<RabbitMqProducer> logger =
    loggerFactory.CreateLogger<RabbitMqProducer>();

var producer = new RabbitMqProducer(logger);

await producer.SendAsync();