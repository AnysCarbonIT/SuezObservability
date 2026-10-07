namespace PersistenceApi.Models
{
    /// <summary>
    /// Représente le message applicatif transporté via RabbitMQ.
    /// </summary>
    public class MessageData
    {
        public Guid Id { get; set; }

        public string? Message { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
