using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;

namespace Shared.Messaging;

public sealed class RabbitMqConnection(ConnectionFactory factory) : IHostedService, IDisposable
{
    private IConnection? _connection;

    public IConnection Connection => _connection
        ?? throw new InvalidOperationException("La connexion RabbitMQ n'est pas démarrée.");

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Une seule connexion au démarrage, réutilisée par les envois ou le consumer.
        _connection = await factory.CreateConnectionAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }

    public void Dispose()
    {
        // Ferme aussi la connexion si le démarrage de l'application a échoué.
        _connection?.Dispose();
    }
}
