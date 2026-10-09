namespace PersistenceApi.Services
{
    /// <summary>
    /// Définit le traitement applicatif à appliquer à un message reçu.
    /// </summary>
    public interface IMessageProcessor
    {
        /// <summary>
        /// Traite un message reçu au format JSON.
        /// </summary>
        Task ProcessAsync(string json, CancellationToken cancellationToken = default);
    }
}
