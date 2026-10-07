using PersistenceApi.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

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
                HostName = "localhost",
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
                    byte[] body = eventArgs.Body.ToArray();

                    string json = Encoding.UTF8.GetString(body);

                    // confirme la réception d'un message RabbitMQ.
                    _logger.LogInformation("Message RabbitMQ reçu. DeliveryTag={DeliveryTag}",eventArgs.DeliveryTag);

                    await _messageProcessor.ProcessAsync(json);

                    await channel.BasicAckAsync(
                        deliveryTag: eventArgs.DeliveryTag,
                        multiple: false,
                        cancellationToken: stoppingToken);

                    // confirme que le message a été traité puis acquitté.
                    _logger.LogInformation("Message traité et acquitté. DeliveryTag={DeliveryTag}",eventArgs.DeliveryTag);
                }
                catch (Exception ex)
                {
                    // enregistre l'erreur avec l'exception complète.
                    _logger.LogError(ex,"Erreur lors du traitement du message RabbitMQ. DeliveryTag={DeliveryTag}",eventArgs.DeliveryTag);

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