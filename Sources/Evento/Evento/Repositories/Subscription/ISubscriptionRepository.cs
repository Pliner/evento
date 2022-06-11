namespace Evento.Repositories.Subscription;

public interface ISubscriptionRepository
{
    Task InsertAsync(Subscription subscription, CancellationToken cancellationToken);
    Task<Subscription[]> SelectActiveAsync(CancellationToken cancellationToken);
    Task<Subscription?> GetActiveByNameAsync(string name, CancellationToken token);
    Task DeactivateAsync(string id, CancellationToken cancellationToken);
}