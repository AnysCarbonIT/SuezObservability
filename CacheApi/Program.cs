using CacheApi.Models;
using CacheApi.Services;
using OpenTelemetry.Trace;
using Shared.Observability;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSuezObservability(
    Telemetry.CacheApiServiceName,
    Telemetry.CacheApiServiceName,
    Telemetry.CacheApiServiceName,
    tracing =>
    {
        tracing.AddAspNetCoreInstrumentation();
    });

string redisConnection =
    builder.Configuration.GetConnectionString("Redis")
    ?? "localhost:6379";

builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(redisConnection));

builder.Services.AddSingleton<RedisMessageStore>();

var app = builder.Build();

app.MapGet(
    "/health",
    () => Results.Ok("CacheApi is running"));

app.MapPost(
    "/cache/messages",
    async (
        MessageData message,
        RedisMessageStore store) =>
    {
        if (message.Id == Guid.Empty || string.IsNullOrWhiteSpace(message.Message) ||
            message.CreatedAt == default || message.CreatedAt.Kind != DateTimeKind.Utc)
        {
            return Results.BadRequest("Le message doit avoir un identifiant, un contenu et une date UTC valides.");
        }

        await store.SaveAsync(message);

        return Results.Ok();
    });

app.Run();
