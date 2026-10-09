using PersistenceApi.Models;
using PersistenceApi.Repositories;
using System.Text.Json;

namespace PersistenceApi.Services
{
    public class MessageProcessor : IMessageProcessor
    {
        private readonly IMessageRepository _messageRepository;
        private readonly CacheApiClient _cacheApiClient;
        private readonly ILogger<MessageProcessor> _logger;

        public MessageProcessor(
            IMessageRepository messageRepository,
            CacheApiClient cacheApiClient,
            ILogger<MessageProcessor> logger)
        {
            _messageRepository = messageRepository;
            _cacheApiClient = cacheApiClient;
            _logger = logger;
        }

        public async Task ProcessAsync(string json, CancellationToken cancellationToken = default)
        {
            var message = JsonSerializer.Deserialize<MessageData>(json);

            if (message == null)
            {
                throw new InvalidMessageException(
                    "Impossible de désérialiser le message RabbitMQ.");
            }

            if (string.IsNullOrWhiteSpace(message.Message))
            {
                throw new InvalidMessageException(
                    "Le message ne peut pas être vide.");
            }

            if (message.Id == Guid.Empty || message.CreatedAt == default || message.CreatedAt.Kind != DateTimeKind.Utc)
            {
                throw new InvalidMessageException("Le message doit avoir un identifiant et une date UTC valides.");
            }

            _logger.LogInformation(
                "Traitement du message {MessageId} - {Message}",
                message.Id,
                message.Message);

            // Première persistance enregistrée dans PostgreSQL
            await _messageRepository.SaveAsync(message, cancellationToken);

            // Deuxième étape : envoi vers CacheApi
            await _cacheApiClient.StoreAsync(message, cancellationToken);
        }
    }
}
