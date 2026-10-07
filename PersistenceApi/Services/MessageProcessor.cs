using PersistenceApi.Models;
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

        public MessageProcessor(ILogger<MessageProcessor> logger)
        {
            _logger = logger;
        }

        public Task ProcessAsync(string json)
        {
            var message = JsonSerializer.Deserialize<MessageData>(json);

            if (message == null)
            {
                throw new InvalidOperationException("Unable to deserialize RabbitMQ message.");
            }

            _logger.LogInformation("Message reçu : {MessageId} - {Message}",message.Id,message.Message);

            return Task.CompletedTask;
        }
    }
}