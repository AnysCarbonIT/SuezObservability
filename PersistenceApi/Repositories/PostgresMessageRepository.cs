using Npgsql;
using PersistenceApi.Models;
using Shared.Observability;
using System.Diagnostics;

namespace PersistenceApi.Repositories
{
    public class PostgresMessageRepository : IMessageRepository
    {
        private readonly PostgresConnectionFactory _connectionFactory;
        private readonly ILogger<PostgresMessageRepository> _logger;
        public PostgresMessageRepository(PostgresConnectionFactory connectionFactory, ILogger<PostgresMessageRepository> logger)
        {
            _connectionFactory = connectionFactory;
            _logger = logger;
        }
        public async Task SaveAsync(MessageData message)
        {
            using var activity = Telemetry.PersistenceApiActivitySource.StartActivity("postgres.insert", ActivityKind.Client);

            activity?.SetTag("db.system", "postgresql");
            activity?.SetTag("db.operation.name", "INSERT");
            activity?.SetTag("db.collection.name", "messages");
            activity?.SetTag("messaging.message.id", message.Id);

            const string sql = @"
                INSERT INTO messages (id, message, created_at)
                VALUES (@id, @message, @created_at)
                ON CONFLICT (id) DO NOTHING;"; // insertion idempotente, si le message existe déjà, aucune nouvelle ligne n'est créée.

            try { 
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync();

            await using var command = new NpgsqlCommand(sql, connection);

            command.Parameters.AddWithValue("id", message.Id);

            command.Parameters.AddWithValue("message", message.Message ?? (object)DBNull.Value);

            command.Parameters.AddWithValue("created_at", message.CreatedAt);

            int rowsAffected = await command.ExecuteNonQueryAsync();

            _logger.LogInformation(
                "Message {MessageId} enregistré dans PostgreSQL. RowsAffected={RowsAffected}",
                message.Id,
                rowsAffected); }
            catch(Exception ex) {activity?.SetStatus(ActivityStatusCode.Error,ex.Message);

                throw;
            }
        }
    }

}
