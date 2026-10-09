using Shared.Models;
using System.Text.Json;

namespace Producer.Services;

public class MessageFactory
{
    public MessageData Create(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Le message ne peut pas être vide.", nameof(content));
        }

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
