using PersistenceApi.Models;
using PersistenceApi.Repositories;
using System.Text.Json;

namespace PersistenceApi.Services
{
    /// <summary>
    /// Traite les messages reçus par l'application.
    /// </summary>
    public class MessageProcessor : IMessageProcessor
    {
        /// <summary>
        /// Désérialise le JSON reçu en MessageData puis applique le traitement.
        /// </summary>
        private readonly ILogger<MessageProcessor> _logger;
        private readonly IMessageRepository _messageRepository;

        public MessageProcessor(ILogger<MessageProcessor> logger, IMessageRepository messageRepository)
        {
            _logger = logger;
            _messageRepository = messageRepository;
        }

        public async Task ProcessAsync(string json)
        {
            var message = JsonSerializer.Deserialize<MessageData>(json);

            if (message == null)
            {
                throw new InvalidOperationException("Impossible de désérialiser le message RabbitMQ.");
            }
            if (string.IsNullOrWhiteSpace(message.Message))
            {
                throw new InvalidOperationException("Le message ne peut pas être vide.");
            }
            _logger.LogInformation("Traitement du message {MessageId} - {Message}", message.Id, message.Message);


            await _messageRepository.SaveAsync(message);
        }
    }
}
