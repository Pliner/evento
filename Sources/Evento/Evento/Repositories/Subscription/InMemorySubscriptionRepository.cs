using System.Collections.Concurrent;

namespace Evento.Repositories.Subscription;

public sealed class InMemorySubscriptionRepository : ISubscriptionRepository
{
    private readonly object mutex = new();
    private readonly ConcurrentDictionary<string, Subscription> primary = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, string>> nameAndVersionIndex = new();

    public Task InsertAsync(Subscription subscription, CancellationToken cancellationToken = default)
    {
        lock (mutex)
        {
            if (primary.ContainsKey(subscription.Id))
                throw new Exception($"Subscription {subscription.Id} already exists");

            var versionIndex = nameAndVersionIndex.GetOrAdd(subscription.Name, _ => new());
            if (versionIndex.ContainsKey(subscription.Version))
                throw new Exception($"Subscription ({subscription.Name}, {subscription.Version}) already exists");

            primary[subscription.Id] = subscription;
            versionIndex[subscription.Version] = subscription.Id;
        }

        return Task.CompletedTask;
    }

    public Task<Subscription[]> SelectActiveAsync(CancellationToken cancellationToken = default)
    {
        // ReSharper disable once InconsistentlySynchronizedField
        var subscriptions = primary.Select(x => x.Value).Where(x => x.Active).ToArray();
        return Task.FromResult(subscriptions);
    }

    public Task<Subscription> GetLastActiveByNameAsync(string name, CancellationToken token = default)
    {
        if (!nameAndVersionIndex.TryGetValue(name, out var versionIndex)) return Task.FromResult<Subscription>(null);

        foreach (var (_, subscriptionId) in versionIndex.OrderByDescending(x => x.Key))
        {
            // ReSharper disable once InconsistentlySynchronizedField
            var subscription = primary[subscriptionId];
            if (subscription.Active)
                return Task.FromResult(subscription);
        }

        return Task.FromResult<Subscription>(null);
    }

    public Task DeactivateAsync(string id, CancellationToken cancellationToken = default)
    {
        lock (mutex)
        {
            if (primary.TryGetValue(id, out var subscription))
                primary[id] = new Subscription
                {
                    Id = subscription.Id,
                    Name = subscription.Name,
                    Version = subscription.Version,
                    CreatedAt = subscription.CreatedAt,
                    Types = subscription.Types,
                    Endpoint = subscription.Endpoint,
                    Active = false
                };
        }

        return Task.CompletedTask;
    }
}