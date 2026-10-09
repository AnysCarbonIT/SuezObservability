using System;
using Shared.Models;

namespace PersistenceApi.Repositories;

public interface IMessageRepository
{
    Task SaveAsync(MessageData message, CancellationToken cancellationToken = default);
}
