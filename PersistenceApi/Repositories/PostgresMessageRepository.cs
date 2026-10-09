using Npgsql;
using Shared.Models;
using PersistenceApi.Observability;
using System.Diagnostics;

namespace PersistenceApi.Repositories;

public class PostgresMessageRepository(
    PostgresConnectionFactory connectionFactory,
    ILogger<PostgresMessageRepository> logger) : IMessageRepository
{
    public async Task SaveAsync(MessageData message, CancellationToken cancellationToken = default)
    {
        using var activity = Telemetry.ActivitySource.StartActivity("postgres.insert", ActivityKind.Client);

        activity?.SetTag("db.system", "postgresql");
        activity?.SetTag("db.operation.name", "INSERT");
        activity?.SetTag("db.collection.name", "messages");
        activity?.SetTag("messaging.message.id", message.Id);

        const string sql = @"
            INSERT INTO messages (id, message, created_at)
            VALUES (@id, @message, @created_at)
            ON CONFLICT (id) DO NOTHING;"; // insertion idempotente, si le message existe déjà, aucune nouvelle ligne n'est créée.

        try
        {
            await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);

            await using var command = new NpgsqlCommand(sql, connection);

            command.Parameters.AddWithValue("id", message.Id);

            command.Parameters.AddWithValue("message", message.Message ?? (object)DBNull.Value);

            command.Parameters.AddWithValue("created_at", message.CreatedAt);

            int rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);

            logger.LogInformation(
                "Message {MessageId} enregistré dans PostgreSQL. RowsAffected={RowsAffected}",
                message.Id,
                rowsAffected);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            throw;
        }
    }
}
