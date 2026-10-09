using PersistenceApi.Models;
using System.Net.Http.Json;

namespace PersistenceApi.Services
{
    public class CacheApiClient
    {
        public const string ClientName = "CacheApi";
        public const string StoreMessagePath = "/cache/messages";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<CacheApiClient> _logger;

        public CacheApiClient(
            IHttpClientFactory httpClientFactory,
            ILogger<CacheApiClient> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task StoreAsync(MessageData message, CancellationToken cancellationToken = default)
        {
            // Récupère le client HTTP configuré pour appeler CacheApi.
            using HttpClient client = _httpClientFactory.CreateClient(ClientName);

            _logger.LogInformation(
                "Envoi du message {MessageId} vers CacheApi",
                message.Id);
            // OpenTelemetry transmet le contexte de trace dans les headers HTTP.
            using HttpResponseMessage response = await client.PostAsJsonAsync(StoreMessagePath, message, cancellationToken);
            // Un échec HTTP remonte au consumer pour déclencher les retries.
            response.EnsureSuccessStatusCode();

            _logger.LogInformation("Message {MessageId} envoyé avec succès vers CacheApi",message.Id);
        }
    }
}
