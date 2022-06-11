namespace Evento.Repositories.Subscription;

public interface ISubscriptionRepository
{
    Task InsertAsync(Subscription subscription, CancellationToken cancellationToken);
    Task<Subscription[]> SelectAsync(CancellationToken cancellationToken);
}