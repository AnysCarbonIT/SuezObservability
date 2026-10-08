using PersistenceApi.Messaging;
using PersistenceApi.Services;
using PersistenceApi.Repositories;
using Shared.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IMessageProcessor, MessageProcessor>();
builder.Services.AddSingleton<PostgresConnectionFactory>();
builder.Services.AddSingleton<IMessageRepository, PostgresMessageRepository>();
builder.Services.AddHostedService<RabbitMqConsumer>();

builder.Services.AddOpenApi();

builder.Services.AddSuezObservability(
    Telemetry.PersistenceApiServiceName,
    Telemetry.PersistenceApiServiceName,
    Telemetry.PersistenceApiServiceName);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () => Results.Ok("PersistenceApi is running"));

app.Run();