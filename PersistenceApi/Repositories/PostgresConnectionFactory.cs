using Npgsql;

namespace PersistenceApi.Repositories
{
    public class PostgresConnectionFactory
    {
        private readonly string _connectionString;

        public PostgresConnectionFactory(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("Postgres") ?? throw new InvalidOperationException("La chaîne de connexion PostgreSQL est manquante.");
        }

        public async Task<NpgsqlConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
        {
            var connection = new NpgsqlConnection(_connectionString);
            try
            {
                await connection.OpenAsync(cancellationToken);

                return connection;
            }
            catch
            {
                await connection.DisposeAsync();
                throw;


            }
        }
    }
}
