using Shared.Models;
using CacheApi.Observability;
using StackExchange.Redis;
using System.Diagnostics;
using System.Text.Json;

namespace CacheApi.Services;

public class RedisMessageStore(
    IConnectionMultiplexer redis,
    ILogger<RedisMessageStore> logger)
{
    public async Task SaveAsync(MessageData message)
    {
        using var activity =
            Telemetry.ActivitySource.StartActivity(
                "redis.set",
                ActivityKind.Client);

        activity?.SetTag("db.system", "redis");
        activity?.SetTag("messaging.message.id", message.Id);

        try
        {
            IDatabase database = redis.GetDatabase();

            string key = $"message:{message.Id}";
            string value = JsonSerializer.Serialize(message);

            bool success =
                await database.StringSetAsync(key, value);

            if (!success)
            {
                throw new InvalidOperationException(
                    "L'écriture Redis a échoué.");
            }

            Telemetry.CacheOperations.Add(1, new KeyValuePair<string, object?>("status", "success"));

            logger.LogInformation(
                "Message {MessageId} enregistré dans Redis avec la clé {RedisKey}",
                message.Id,
                key);
        }
        catch (Exception ex)
        {
            Telemetry.CacheOperations.Add(1, new KeyValuePair<string, object?>("status", "failed"));

            activity?.SetStatus(
                ActivityStatusCode.Error,
                ex.Message);

            logger.LogError(
                ex,
                "Erreur lors de l'enregistrement du message {MessageId} dans Redis",
                message.Id);

            throw;
        }
    }
}
