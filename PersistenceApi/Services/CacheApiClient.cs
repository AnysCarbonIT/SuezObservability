using Shared.Models;
using System.Net.Http.Json;

namespace PersistenceApi.Services;

public class CacheApiClient(
    IHttpClientFactory httpClientFactory,
    ILogger<CacheApiClient> logger)
{
    public const string ClientName = "CacheApi";
    public const string StoreMessagePath = "/cache/messages";

    public async Task StoreAsync(MessageData message, CancellationToken cancellationToken = default)
    {
        // Récupère le client HTTP configuré pour appeler CacheApi.
        using HttpClient client = httpClientFactory.CreateClient(ClientName);

        logger.LogInformation(
            "Envoi du message {MessageId} vers CacheApi",
            message.Id);
        // OpenTelemetry transmet le contexte de trace dans les headers HTTP.
        using HttpResponseMessage response = await client.PostAsJsonAsync(StoreMessagePath, message, cancellationToken);
        response.EnsureSuccessStatusCode();

        logger.LogInformation("Message {MessageId} envoyé avec succès vers CacheApi",message.Id);
    }
}
