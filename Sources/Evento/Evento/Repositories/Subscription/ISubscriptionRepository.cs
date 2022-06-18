namespace Evento.Repositories.Subscription;

public interface ISubscriptionRepository
{
    Task InsertAsync(Subscription subscription, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Subscription>> SelectActiveAsync(CancellationToken cancellationToken = default);
    Task<Subscription?> GetLastVersionByNameAsync(string name, CancellationToken cancellationToken = default);
    Task DeactivateAsync(string id, CancellationToken cancellationToken = default);
}