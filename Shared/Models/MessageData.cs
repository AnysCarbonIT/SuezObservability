namespace Shared.Models;

// Le même message circule dans RabbitMQ, PostgreSQL et CacheApi.
public record MessageData
{
    public Guid Id { get; init; }
    public string? Message { get; init; }
    public DateTime CreatedAt { get; init; }
}
