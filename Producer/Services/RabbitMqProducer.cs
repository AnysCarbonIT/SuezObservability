using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using Producer.Observability;
using RabbitMQ.Client;
using Shared.Messaging;
using System.Diagnostics;
using System.Text;

namespace Producer.Services;

public class RabbitMqProducer(
    RabbitMqConnection rabbitMqConnection,
    MessageFactory messageFactory,
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqProducer> logger)
{
    private readonly string _queueName = options.Value.QueueName;

    public async Task SendAsync(CancellationToken cancellationToken = default)
    {
        Console.Write("Message à envoyer : ");
        string? content = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(content))
        {
            logger.LogWarning("Aucun message envoyé : la saisie est vide.");
            return;
        }

        try
        {
            // Le channel est propre à l'envoi, la connexion reste ouverte dans le singleton.
            await using var channel = await rabbitMqConnection.Connection.CreateChannelAsync(
                cancellationToken: cancellationToken);

            await channel.QueueDeclareAsync(
                queue: _queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken);

            var message = messageFactory.Create(content);
            byte[] body = Encoding.UTF8.GetBytes(messageFactory.Serialize(message));

            // Ajout du span Producer pour suivre le message.
            using var activity = Telemetry.ActivitySource.StartActivity("message.publish", ActivityKind.Producer);
            activity?.SetTag("messaging.system", "rabbitmq");
            activity?.SetTag("messaging.destination.name", _queueName);
            activity?.SetTag("messaging.message.id", message.Id);

            var properties = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                MessageId = message.Id.ToString(),
                Headers = new Dictionary<string, object?>()
            };

            // Transmet le contexte de trace au consumer dans les headers RabbitMQ.
            var propagationContext = new PropagationContext(activity?.Context ?? default, Baggage.Current);
            Propagators.DefaultTextMapPropagator.Inject(propagationContext, properties.Headers, (headers, key, value) =>
            {
                headers[key] = Encoding.UTF8.GetBytes(value);
            });

            logger.LogInformation("Envoi du message {MessageId} vers la queue {QueueName}", message.Id, _queueName);
            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: _queueName,
                mandatory: false,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);
            logger.LogInformation("Message {MessageId} envoyé avec succès vers RabbitMQ", message.Id);
            logger.LogInformation(
                "Message {MessageId} envoyé. TraceId={TraceId}, SpanId={SpanId}",
                message.Id,
                activity?.TraceId.ToString(),
                activity?.SpanId.ToString());
        }
        catch (Exception ex)
        {
            // Conserve l'exception en cas d'échec réseau ou RabbitMQ.
            logger.LogError(ex, "Erreur lors de l'envoi du message vers RabbitMQ");
            throw;
        }
    }
}
