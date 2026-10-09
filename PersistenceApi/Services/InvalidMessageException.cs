namespace PersistenceApi.Services;

// Erreur de contenu : rejouer le même message ne corrigera pas ses données.
public class InvalidMessageException : Exception
{
    public InvalidMessageException(string message) : base(message)
    {
    }
}
