using CacheApi.Models;
using Shared.Observability;
using StackExchange.Redis;
using System.Diagnostics;
using System.Text.Json;

namespace CacheApi.Services
{
    public class RedisMessageStore
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RedisMessageStore> _logger;

        public RedisMessageStore(
            IConnectionMultiplexer redis,
            ILogger<RedisMessageStore> logger)
        {
            _redis = redis;
            _logger = logger;
        }

        public async Task SaveAsync(MessageData message)
        {
            using var activity =
                Telemetry.CacheApiActivitySource.StartActivity(
                    "redis.set",
                    ActivityKind.Client);

            activity?.SetTag("db.system", "redis");
            activity?.SetTag("messaging.message.id", message.Id);

            try
            {
                IDatabase database = _redis.GetDatabase();

                string key = $"message:{message.Id}";
                string value = JsonSerializer.Serialize(message);

                bool success =
                    await database.StringSetAsync(key, value);

                if (!success)
                {
                    throw new InvalidOperationException(
                        "L'écriture Redis a échoué.");
                }

                Telemetry.CacheWrites.Add(1);

                _logger.LogInformation(
                    "Message {MessageId} enregistré dans Redis avec la clé {RedisKey}",
                    message.Id,
                    key);
            }
            catch (Exception ex)
            {
                Telemetry.CacheFailures.Add(1);

                activity?.SetStatus(
                    ActivityStatusCode.Error,
                    ex.Message);

                _logger.LogError(
                    ex,
                    "Erreur lors de l'enregistrement du message {MessageId} dans Redis",
                    message.Id);

                throw;
            }
        }
    }
}