using Npgsql;
using OpenTelemetry.Trace;
using PersistenceApi.Messaging;
using PersistenceApi.Repositories;
using PersistenceApi.Services;
using Shared.Observability;
using Shared.Messaging;
using PersistenceApi.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRabbitMq(builder.Configuration, Telemetry.ServiceName);
// La connexion démarre une fois et reste ouverte pour le consumer.
builder.Services.AddSingleton<RabbitMqConnection>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<RabbitMqConnection>());

builder.Services.AddSingleton<IMessageProcessor, MessageProcessor>();
builder.Services.AddSingleton<PostgresConnectionFactory>();
builder.Services.AddSingleton<IMessageRepository, PostgresMessageRepository>();

builder.Services.AddOpenApi();

builder.Services.AddSuezObservability(
    Telemetry.ServiceName,
    Telemetry.ServiceName,
    Telemetry.ServiceName,
    tracing =>
    {
        tracing
            .AddNpgsql() // Trace les appels PostgreSQL.
            .AddAspNetCoreInstrumentation() // Trace les requêtes HTTP reçues par l'API.
            .AddHttpClientInstrumentation(); // Trace les appels HTTP vers CacheApi et propage le TraceId.
    });
// OpenTelemetry est démarré avant de consommer les messages déjà en attente.
builder.Services.AddHostedService<RabbitMqConsumer>();
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
