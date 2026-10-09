using Npgsql;
using OpenTelemetry.Trace;
using PersistenceApi.Messaging;
using PersistenceApi.Repositories;
using PersistenceApi.Services;
using Shared.Observability;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IMessageProcessor, MessageProcessor>();
builder.Services.AddSingleton<PostgresConnectionFactory>();
builder.Services.AddSingleton<IMessageRepository, PostgresMessageRepository>();
builder.Services.AddHostedService<RabbitMqConsumer>();

builder.Services.AddOpenApi();

builder.Services.AddSuezObservability(
    Telemetry.PersistenceApiServiceName,
    Telemetry.PersistenceApiServiceName,
    Telemetry.PersistenceApiServiceName,
    tracing =>
    {
        tracing
            .AddNpgsql() // Trace les appels PostgreSQL.
            .AddAspNetCoreInstrumentation() // Trace les requêtes HTTP reçues par l'API.
            .AddHttpClientInstrumentation(); // Trace les appels HTTP vers CacheApi et propage le TraceId.
    });
// Configure l'adresse utilisée par PersistenceApi pour appeler CacheApi.
builder.Services.AddHttpClient(
    CacheApiClient.ClientName,
    client =>
    {
        string baseUrl =
            builder.Configuration["CacheApi:BaseUrl"]
            ?? "http://localhost:5126";

        client.BaseAddress = new Uri(baseUrl);
        client.Timeout = TimeSpan.FromSeconds(10);
    });

builder.Services.AddSingleton<CacheApiClient>();
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () => Results.Ok("PersistenceApi is running"));

app.Run();
