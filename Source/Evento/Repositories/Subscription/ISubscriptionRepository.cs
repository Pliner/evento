namespace Evento.Repositories.Subscription;

public sealed record NewSubscriptionData(
    string Name,
    int Version,
    IReadOnlySet<string> Types,
    string Endpoint
);

public interface ISubscriptionRepository
{
    Task AddAsync(NewSubscriptionData subscriptionData, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> SelectNamesAsync(CancellationToken cancellationToken = default);
    Task<Subscription?> TryGetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task SetNotActiveAsync(Subscription subscription, CancellationToken cancellationToken = default);
}