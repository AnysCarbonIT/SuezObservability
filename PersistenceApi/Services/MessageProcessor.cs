using Shared.Models;
using PersistenceApi.Repositories;
using System.Text.Json;

namespace PersistenceApi.Services;

public class MessageProcessor(
    IMessageRepository messageRepository,
    CacheApiClient cacheApiClient,
    ILogger<MessageProcessor> logger) : IMessageProcessor
{
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

        logger.LogInformation(
            "Traitement du message {MessageId} - {Message}",
            message.Id,
            message.Message);

        // Première persistance enregistrée dans PostgreSQL
        await messageRepository.SaveAsync(message, cancellationToken);

        // Deuxième étape : envoi vers CacheApi
        await cacheApiClient.StoreAsync(message, cancellationToken);
    }
}
