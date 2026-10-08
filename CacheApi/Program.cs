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
        await store.SaveAsync(message);

        return Results.Ok();
    });

app.Run();