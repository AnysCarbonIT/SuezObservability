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

        public async Task ProcessAsync(string json)
        {
            var message = JsonSerializer.Deserialize<MessageData>(json);

            if (message == null)
            {
                throw new InvalidOperationException(
                    "Impossible de désérialiser le message RabbitMQ.");
            }

            if (string.IsNullOrWhiteSpace(message.Message))
            {
                throw new InvalidOperationException(
                    "Le message ne peut pas être vide.");
            }

            _logger.LogInformation(
                "Traitement du message {MessageId} - {Message}",
                message.Id,
                message.Message);

            // Première persistance enregistrée dans PostgreSQL
            await _messageRepository.SaveAsync(message);

            // Deuxième étape : envoi vers CacheApi
            await _cacheApiClient.StoreAsync(message);
        }
    }
}