using PersistenceApi.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using OpenTelemetry.Context.Propagation;
using PersistenceApi.Observability;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text.Json;
using Shared.Messaging;

namespace PersistenceApi.Messaging;

/// <summary>
/// Écoute la queue RabbitMQ et transmet les messages reçus
/// au traitement applicatif.
/// </summary>
public class RabbitMqConsumer(
    RabbitMqConnection rabbitMqConnection,
    IOptions<RabbitMqOptions> options,
    IMessageProcessor messageProcessor,
    ILogger<RabbitMqConsumer> logger) : BackgroundService
{
    private readonly string _queueName = options.Value.QueueName;

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        // indique que le consumer démarre et précise la queue écoutée.
        logger.LogInformation(
            "Démarrage du consumer RabbitMQ sur la queue {QueueName}",
            _queueName);

        var connection = rabbitMqConnection.Connection;

        await using var channel =
            await connection.CreateChannelAsync(
                cancellationToken: stoppingToken);

        await channel.QueueDeclareAsync(
            queue: _queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: stoppingToken);

        // Un seul message traité à la fois.
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
                    Telemetry.ActivitySource.StartActivity(
                        "message.consume",
                        ActivityKind.Consumer,
                        parentContext.ActivityContext);

                activity?.SetTag("messaging.system", "rabbitmq");
                activity?.SetTag("messaging.destination.name", _queueName);
                activity?.SetTag(
                    "messaging.message.id",
                    eventArgs.BasicProperties.MessageId);

                // confirme la réception d'un message RabbitMQ.
                logger.LogInformation(
                    "Message RabbitMQ reçu. DeliveryTag={DeliveryTag}, TraceId={TraceId}, SpanId={SpanId}",
                    eventArgs.DeliveryTag,
                    activity?.TraceId.ToString(),
                    activity?.SpanId.ToString());

                byte[] body = eventArgs.Body.ToArray();

                string json = Encoding.UTF8.GetString(body);

                await messageProcessor.ProcessAsync(json, stoppingToken);

                // ACK seulement quand PostgreSQL et le cache ont réussi.
                await channel.BasicAckAsync(
                    deliveryTag: eventArgs.DeliveryTag,
                    multiple: false,
                    cancellationToken: stoppingToken);

                // métrique : traitement complet réussi
                Telemetry.Messages.Add(1, new KeyValuePair<string, object?>("status", "processed"));

                logger.LogInformation(
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

                logger.LogWarning(
                    ex,
                    "Message RabbitMQ invalide. DeliveryTag={DeliveryTag}",
                    eventArgs.DeliveryTag);

                // Message invalide : on le rejette sans le remettre dans la queue.
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

                logger.LogError(
                    ex,
                    "Erreur technique lors du traitement du message RabbitMQ. DeliveryTag={DeliveryTag}",
                    eventArgs.DeliveryTag);

                // Échec du traitement : on rejette le message, sans nouvelle tentative.
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
            queue: _queueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        await Task.Delay(
            Timeout.Infinite,
            stoppingToken);
    }
}
