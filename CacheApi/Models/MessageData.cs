namespace CacheApi.Models
{
    public class MessageData
    {
        public Guid Id { get; set; }

        public string? Message { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
