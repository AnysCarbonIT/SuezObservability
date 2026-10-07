using Producer.Models;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace Producer.Services
{
    public class RabbitMqProducer
    {
        private const string QueueName = "suez-messages";

        public async Task SendAsync()
        {
            var factory = new ConnectionFactory
            {
                HostName = "localhost",
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

            var message = new MessageData
            {
                Id = Guid.NewGuid(),
                Message = "Message de test SUEZ",
                CreatedAt = DateTime.UtcNow
            };

            string json = JsonSerializer.Serialize(message);
            byte[] body = Encoding.UTF8.GetBytes(json);

            var properties = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                MessageId = message.Id.ToString()
            };

            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: QueueName,
                mandatory: false,
                basicProperties: properties,
                body: body);

            Console.WriteLine("Message envoyé dans RabbitMQ :");
            Console.WriteLine(json);
        }
    }
}