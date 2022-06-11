using System.Collections.Concurrent;

namespace Evento.Repositories.Subscription;

public sealed class InMemorySubscriptionRepository : ISubscriptionRepository
{
    private readonly ConcurrentDictionary<string, Subscription> primary = new();

    public Task InsertAsync(Subscription subscription, CancellationToken cancellationToken)
    {
        if (!primary.TryAdd(subscription.Id, subscription))
            throw new Exception($"Subscription {subscription.Id} already exists");
        return Task.CompletedTask;
    }

    public Task<Subscription[]> SelectAsync(CancellationToken cancellationToken)
    {
        var subscriptions = primary.Select(x => x.Value).ToArray();
        return Task.FromResult(subscriptions);
    }
}