using Producer.Models;
using System.Text.Json;

namespace Producer.Services
{
    public class MessageFactory
    {
        public MessageData Create(string content)
        {
            return new MessageData
            {
                Id = Guid.NewGuid(),
                Message = content,
                CreatedAt = DateTime.UtcNow
            };
        }

        public string Serialize(MessageData message)
        {
            return JsonSerializer.Serialize(message);
        }
    }
}