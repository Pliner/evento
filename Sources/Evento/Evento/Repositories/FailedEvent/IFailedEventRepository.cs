namespace Evento.Repositories.FailedEvent;

public interface IFailedEventRepository
{
    Task InsertAsync(FailedEvent @event, CancellationToken cancellationToken = default);
    Task<FailedEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpdateResolutionStatusByIdAsync(Guid id, FailedEventResolutionStatus status, CancellationToken cancellationToken = default);
}