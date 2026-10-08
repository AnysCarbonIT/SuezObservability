using PersistenceApi.Models;
using System.Net.Http.Json;

namespace PersistenceApi.Services
{
    public class CacheApiClient
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<CacheApiClient> _logger;

        public CacheApiClient(
            IHttpClientFactory httpClientFactory,
            ILogger<CacheApiClient> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task StoreAsync(MessageData message)
        {
            // Récupère le client HTTP configuré pour appeler CacheApi.
            HttpClient client = _httpClientFactory.CreateClient("CacheApi");

            _logger.LogInformation(
                "Envoi du message {MessageId} vers CacheApi",
                message.Id);
            // Envoie vers CacheApi méthode POST OpenTelemetry propage automatiquement la trace dans les headers HTTP
            HttpResponseMessage response = await client.PostAsJsonAsync("/cache/messages",message);
            // Génère une erreur si CacheApi ne répond pas avec un code HTTP de succès
            response.EnsureSuccessStatusCode();

            _logger.LogInformation("Message {MessageId} envoyé avec succès vers CacheApi",message.Id);
        }
    }
}