using PersistenceApi.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using OpenTelemetry.Context.Propagation;
using Shared.Observability;
using System.Diagnostics;
using System.Text.Json;
using Shared.Messaging;

namespace PersistenceApi.Messaging
{
    /// <summary>
    /// Écoute la queue RabbitMQ et transmet les messages reçus
    /// au traitement applicatif.
    /// </summary>
    public class RabbitMqConsumer : BackgroundService
    {
        private const string QueueName = MessageQueues.Main;
        private const int MaxAttempts = 3;

        private readonly IMessageProcessor _messageProcessor;
        private readonly ILogger<RabbitMqConsumer> _logger;

        public RabbitMqConsumer(
            IMessageProcessor messageProcessor,
            ILogger<RabbitMqConsumer> logger)
        {
            _messageProcessor = messageProcessor;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            // indique que le consumer démarre et précise la queue écoutée.
            _logger.LogInformation(
                "Démarrage du consumer RabbitMQ sur la queue {QueueName}",
                QueueName);

            var factory = new ConnectionFactory
            {
                HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost",
                UserName = Environment.GetEnvironmentVariable("RABBITMQ_USER") ?? "guest",
                Password = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD") ?? "guest",
                ClientProvidedName = "SuezObservability.PersistenceApi"
            };

            await using var connection =
                await factory.CreateConnectionAsync(stoppingToken);

            await using var channel =
                await connection.CreateChannelAsync(
                    cancellationToken: stoppingToken);

            await channel.QueueDeclareAsync(
                queue: MessageQueues.Failed,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: stoppingToken);

            await channel.QueueDeclareAsync(
                queue: QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: MessageQueues.Arguments,
                cancellationToken: stoppingToken);

            // Un seul message en cours : les retries ne saturent pas les dépendances.
            await channel.BasicQosAsync(0, 1, false, stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.ReceivedAsync += async (_, eventArgs) =>
            {
                Activity? activity = null;
                long startedAt = Stopwatch.GetTimestamp();

                try
                {
                    // Récupération du contexte OpenTelemetry envoyé par le Producer
                    var propagator = Propagators.DefaultTextMapPropagator;

                    var parentContext = propagator.Extract(
                        default,
                        eventArgs.BasicProperties.Headers,
                        (headers, key) =>
                        {
                            if (headers != null &&
                                headers.TryGetValue(key, out var value))
                            {
                                if (value is byte[] bytes)
                                {
                                    return new[]
                                    {
                                        Encoding.UTF8.GetString(bytes)
                                    };
                                }

                                if (value is ReadOnlyMemory<byte> memory)
                                {
                                    return new[]
                                    {
                                        Encoding.UTF8.GetString(memory.Span)
                                    };
                                }
                            }

                            return Array.Empty<string>();
                        });

                    // Création du span Consumer rattaché à la trace du Producer
                    activity =
                        Telemetry.PersistenceApiActivitySource.StartActivity(
                            "message.consume",
                            ActivityKind.Consumer,
                            parentContext.ActivityContext);

                    activity?.SetTag("messaging.system", "rabbitmq");
                    activity?.SetTag("messaging.destination.name", QueueName);
                    activity?.SetTag(
                        "messaging.message.id",
                        eventArgs.BasicProperties.MessageId);

                    // confirme la réception d'un message RabbitMQ.
                    _logger.LogInformation(
                        "Message RabbitMQ reçu. DeliveryTag={DeliveryTag}, TraceId={TraceId}, SpanId={SpanId}",
                        eventArgs.DeliveryTag,
                        activity?.TraceId.ToString(),
                        activity?.SpanId.ToString());

                    byte[] body = eventArgs.Body.ToArray();

                    string json = Encoding.UTF8.GetString(body);

                    // On garde le message non acquitté pendant les trois tentatives.
                    // PostgreSQL et Redis réutilisent le même Id lors d'une reprise.
                    for (int attempt = 1; attempt <= MaxAttempts; attempt++)
                    {
                        try
                        {
                            await _messageProcessor.ProcessAsync(json, stoppingToken);
                            break;
                        }
                        // On réessaie seulement les erreurs techniques, sauf si l'application s'arrête.
                        catch (Exception ex) when (
                            ex is not InvalidMessageException &&
                            ex is not JsonException &&
                            !stoppingToken.IsCancellationRequested &&
                            attempt < MaxAttempts)
                        {
                            _logger.LogWarning(ex,
                                "Tentative {Attempt}/{MaxAttempts} échouée. Nouvel essai dans 5 secondes.",
                                attempt, MaxAttempts);
                            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                        }
                    }

                    // ACK seulement quand PostgreSQL et le cache ont réussi.
                    await channel.BasicAckAsync(
                        deliveryTag: eventArgs.DeliveryTag,
                        multiple: false,
                        cancellationToken: stoppingToken);

                    // métrique : traitement complet réussi
                    Telemetry.Messages.Add(1, new KeyValuePair<string, object?>("status", "processed"));

                    _logger.LogInformation(
                        "Message traité et acquitté. DeliveryTag={DeliveryTag}",
                        eventArgs.DeliveryTag);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // À l'arrêt, pas d'ACK : RabbitMQ pourra redistribuer le message.
                }
                catch (Exception ex) when (ex is InvalidMessageException or JsonException)
                {
                    // métrique : traitement fonctionnel en échec
                    Telemetry.Messages.Add(1, new KeyValuePair<string, object?>("status", "failed"));

                    activity?.SetStatus(ActivityStatusCode.Error,ex.Message);

                    _logger.LogWarning(
                        ex,
                        "Message RabbitMQ invalide. DeliveryTag={DeliveryTag}",
                        eventArgs.DeliveryTag);

                    // Données invalides : pas de retry, direction la file d'échec.
                    await channel.BasicNackAsync(
                        deliveryTag: eventArgs.DeliveryTag,
                        multiple: false,
                        requeue: false,
                        cancellationToken: stoppingToken);
                }
                catch (Exception ex)
                {
                    // erreur technique : PostgreSQL, réseau, API distante...
                    Telemetry.Messages.Add(1, new KeyValuePair<string, object?>("status", "failed"));

                    activity?.SetStatus(ActivityStatusCode.Error,ex.Message);

                    _logger.LogError(
                        ex,
                        "Erreur technique lors du traitement du message RabbitMQ. DeliveryTag={DeliveryTag}",
                        eventArgs.DeliveryTag);

                    // Après les trois tentatives, RabbitMQ conserve le message dans la file d'échec.
                    await channel.BasicNackAsync(
                        deliveryTag: eventArgs.DeliveryTag,
                        multiple: false,
                        requeue: false,
                        cancellationToken: stoppingToken);
                }
                finally
                {
                    Telemetry.MessageDuration.Record(Stopwatch.GetElapsedTime(startedAt).TotalSeconds);
                    activity?.Dispose();
                }
            };

            await channel.BasicConsumeAsync(
                queue: QueueName,
                autoAck: false,
                consumer: consumer,
                cancellationToken: stoppingToken);

            await Task.Delay(
                Timeout.Infinite,
                stoppingToken);
        }
    }
}
