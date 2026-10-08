using PersistenceApi.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using OpenTelemetry.Context.Propagation;
using Shared.Observability;
using System.Diagnostics;

namespace PersistenceApi.Messaging
{
    /// <summary>
    /// Écoute la queue RabbitMQ et transmet les messages reçus
    /// au traitement applicatif.
    /// </summary>
    public class RabbitMqConsumer : BackgroundService
    {
        private const string QueueName = "suez-messages";

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
            //  indique que le consumer démarre et précise la queue écoutée.
            _logger.LogInformation(
                "Démarrage du consumer RabbitMQ sur la queue {QueueName}",
                QueueName);

            var factory = new ConnectionFactory
            {
                HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost",
                UserName = "guest",
                Password = "guest",
                ClientProvidedName = "SuezObservability.PersistenceApi"
            };

            await using var connection =
                await factory.CreateConnectionAsync(stoppingToken);

            await using var channel =
                await connection.CreateChannelAsync(
                    cancellationToken: stoppingToken);

            await channel.QueueDeclareAsync(
                queue: QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.ReceivedAsync += async (_, eventArgs) =>
            {
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
                    using var activity =
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

                    // await pour futuir persistance postgrsql
                    await _messageProcessor.ProcessAsync(json);

                    // await pour acquitter le message RabbitMQ après bon traitement
                    await channel.BasicAckAsync(
                        deliveryTag: eventArgs.DeliveryTag,
                        multiple: false,
                        cancellationToken: stoppingToken);
                    // Metrique :  traitement complet du message a réussi.
                    Telemetry.ProcessedMessages.Add(1);
                    // confirme que le message a été traité puis acquitté.
                    _logger.LogInformation("Message traité et acquitté. DeliveryTag={DeliveryTag}",eventArgs.DeliveryTag);

                }
                catch (Exception ex)
                {
                    // enregistre l'erreur avec l'exception complète.
                    _logger.LogError(ex,"Erreur lors du traitement du message RabbitMQ. DeliveryTag={DeliveryTag}",eventArgs.DeliveryTag);

                    // Metrique :  traitement du message a échoué.
                    Telemetry.FailedMessages.Add(1);

                    throw;
                }
            };

            await channel.BasicConsumeAsync(
                queue: QueueName,
                autoAck: false,
                consumer: consumer,
                cancellationToken: stoppingToken);

            await Task.Delay(Timeout.Infinite,stoppingToken);
        }
    }
}