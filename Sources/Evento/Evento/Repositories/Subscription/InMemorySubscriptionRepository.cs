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

    public async Task<IReadOnlyList<Subscription>> SelectActiveAsync(CancellationToken cancellationToken = default)
    {
        // ReSharper disable once InconsistentlySynchronizedField
        return primary.Select(x => x.Value).Where(x => x.Active).ToList();
    }

    public async Task<Subscription?> GetLastVersionByNameAsync(string name, CancellationToken token = default)
    {
        if (!nameAndVersionIndex.TryGetValue(name, out var versionIndex)) return null;

        foreach (var (_, subscriptionId) in versionIndex.OrderByDescending(x => x.Key))
        {
            // ReSharper disable once InconsistentlySynchronizedField
            return primary[subscriptionId];
        }

        return null;
    }

    public async Task DeactivateAsync(string id, CancellationToken cancellationToken = default)
    {
        lock (mutex)
        {
            if (primary.TryGetValue(id, out var subscription))
                primary[id] = subscription with { Active = false };
        }
    }
}