namespace Shared.Messaging
{
    public static class MessageQueues
    {
        public const string Main = "suez-messages";
        public const string Failed = "suez-messages.failed";

        // Les messages rejetés sont redirigés vers la file d'échec.
        public static Dictionary<string, object?> Arguments => new()
        {
            ["x-dead-letter-exchange"] = "",
            ["x-dead-letter-routing-key"] = Failed
        };
    }
}
