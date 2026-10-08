using Microsoft.Extensions.Logging;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;
using Shared.Observability;
using System.Diagnostics;
using System.Text;
using OpenTelemetry;



namespace Producer.Services
{
    public class RabbitMqProducer
    {
        private const string QueueName = "suez-messages";

        private readonly ILogger<RabbitMqProducer> _logger;

        public RabbitMqProducer(ILogger<RabbitMqProducer> logger)
        {
            _logger = logger;
        }


        public async Task SendAsync()
        {
            try
            {
                    var factory = new ConnectionFactory
                {
                        HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost",
                        UserName = "guest",
                    Password = "guest",
                    ClientProvidedName = "SuezObservability.Producer"
                };

                await using var connection = await factory.CreateConnectionAsync();
                await using var channel = await connection.CreateChannelAsync();

                await channel.QueueDeclareAsync(
                    queue: QueueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);
                var messageFactory = new MessageFactory();

                Console.Write("Message à envoyer : ");
                string? content = Console.ReadLine();
                var message = messageFactory.Create(content ?? string.Empty);

                string json = messageFactory.Serialize(message);
                byte[] body = Encoding.UTF8.GetBytes(json);
                
                // Ajout span opentelemetry
                using var activity = Telemetry.ProducerActivitySource.StartActivity("message.publish", ActivityKind.Producer);
                activity?.SetTag("messaging.system", "rabbitmq");
                activity?.SetTag("messaging.destination.name", QueueName);
                activity?.SetTag("messaging.message.id", message.Id);

                var properties = new BasicProperties
                {
                    ContentType = "application/json",
                    DeliveryMode = DeliveryModes.Persistent,
                    MessageId = message.Id.ToString(),
                    Headers = new Dictionary<string, object?>()
                };
                var propagator = Propagators.DefaultTextMapPropagator;
                var propagationContext = new PropagationContext(activity?.Context ?? default, Baggage.Current);
                propagator.Inject(propagationContext, properties.Headers, (headers, key, value) => {headers[key] = Encoding.UTF8.GetBytes(value);
                }); 

                _logger.LogInformation("Envoi du message {MessageId} vers la queue {QueueName}", message.Id, QueueName);
                await channel.BasicPublishAsync(
                    exchange: string.Empty,
                    routingKey: QueueName,
                    mandatory: false,
                    basicProperties: properties,
                    body: body);
                _logger.LogInformation("Message {MessageId} envoyé avec succès vers RabbitMQ", message.Id);
                _logger.LogInformation(
                        "Message {MessageId} envoyé. TraceId={TraceId}, SpanId={SpanId}",
                        message.Id,
                        activity?.TraceId.ToString(),
                        activity?.SpanId.ToString());
            }
             catch (Exception ex)
            {
                // conserve l'exception en cas d'échec réseau ou RabbitMQ.
                _logger.LogError(
                    ex,
                    "Erreur lors de l'envoi du message vers RabbitMQ");

                throw;
            }
        }
    }
}