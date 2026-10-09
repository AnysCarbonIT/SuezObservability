using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using PersistenceApi.Models;
using PersistenceApi.Repositories;
using PersistenceApi.Services;

namespace PersistenceApi.Tests
{
    public class MessageProcessorTests
    {
        private static MessageData ValidMessage() => new()
        {
            Id = Guid.NewGuid(), Message = "Message de test", CreatedAt = DateTime.UtcNow
        };

        private static MessageProcessor CreateProcessor(TestRepository repository, TestHandler handler)
        {
            var client = new CacheApiClient(new TestClientFactory(handler), NullLogger<CacheApiClient>.Instance);
            return new MessageProcessor(repository, client, NullLogger<MessageProcessor>.Instance);
        }

        [Test]
        public void InvalidMessage_ShouldNotCallPostgresOrCache()
        {
            var calls = new List<string>();
            var processor = CreateProcessor(new TestRepository(calls), new TestHandler(calls));
            var message = ValidMessage();
            message.Message = "";

            Assert.ThrowsAsync<InvalidMessageException>(() => processor.ProcessAsync(JsonSerializer.Serialize(message)));
            Assert.That(calls, Is.Empty);
        }

        [Test]
        public void PostgresFailure_ShouldNotCallCache()
        {
            var calls = new List<string>();
            var repository = new TestRepository(calls) { Fail = true };
            var processor = CreateProcessor(repository, new TestHandler(calls));

            Assert.ThrowsAsync<IOException>(() => processor.ProcessAsync(JsonSerializer.Serialize(ValidMessage())));
            Assert.That(calls, Is.EqualTo(new[] { "postgres" }));
        }

        [Test]
        public async Task ValidMessage_ShouldCallPostgresThenCache()
        {
            var calls = new List<string>();
            var handler = new TestHandler(calls);
            var processor = CreateProcessor(new TestRepository(calls), handler);
            await processor.ProcessAsync(JsonSerializer.Serialize(ValidMessage()));

            Assert.That(calls, Is.EqualTo(new[] { "postgres", "cache" }));
        }

        // Remplace PostgreSQL : note l'appel et peut simuler une panne.
        private class TestRepository : IMessageRepository
        {
            private readonly List<string> _calls;
            public TestRepository(List<string> calls)
            {
                _calls = calls;
            }

            public bool Fail { get; set; }
            public Task SaveAsync(MessageData message, CancellationToken cancellationToken = default)
            {
                _calls.Add("postgres");
                if (Fail)
                {
                    throw new IOException("PostgreSQL indisponible");
                }
                return Task.CompletedTask;
            }
        }

        // Remplace la réponse de CacheApi : aucun serveur HTTP à lancer pour les tests.
        private class TestHandler : HttpMessageHandler
        {
            private readonly List<string> _calls;
            public TestHandler(List<string> calls)
            {
                _calls = calls;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                _calls.Add("cache");
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            }
        }

        // Fournit le client HTTP qui utilise notre réponse de test.
        private class TestClientFactory : IHttpClientFactory
        {
            private readonly TestHandler _handler;
            public TestClientFactory(TestHandler handler)
            {
                _handler = handler;
            }

            public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false)
            {
                BaseAddress = new Uri("http://cacheapi")
            };
        }
    }
}
