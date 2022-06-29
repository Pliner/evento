using System.Collections.Concurrent;

namespace Evento.Repositories.FailedEvent;

public class InMemoryFailedEventRepository : IFailedEventRepository
{
    private readonly ConcurrentDictionary<Guid, FailedEvent> primary = new();

    public async Task InsertAsync(FailedEvent @event, CancellationToken cancellationToken)
    {
        if (!primary.TryAdd(@event.Id, @event))
            throw new Exception($"Failed event {@event.Id} already exists");
    }

    public async Task<FailedEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return primary.TryGetValue(id, out var @event) ? @event : null;
    }

    public async Task UpdateResolutionStatusByIdAsync(Guid id, FailedEventResolutionStatus status, CancellationToken cancellationToken)
    {
        if (primary.TryGetValue(id, out var @event) && @event.Status == FailedEventResolutionStatus.Unresolved)
            primary[id] = @event with { Status = status };
    }
}