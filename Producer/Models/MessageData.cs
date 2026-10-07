using System;
using System.Collections.Generic;
using System.Text;

namespace Producer.Models
{
    public class MessageData
    {
        public Guid Id { get; set; }

        public string? Message { get; set; }

        public DateTime CreatedAt { get; set; }

    }
}
