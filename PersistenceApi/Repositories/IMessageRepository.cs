using System;
using PersistenceApi.Models;

namespace PersistenceApi.Repositories
{
    public interface IMessageRepository
    {
        Task SaveAsync(MessageData message);
    }
}
