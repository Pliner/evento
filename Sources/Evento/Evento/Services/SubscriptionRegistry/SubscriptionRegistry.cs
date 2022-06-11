using System.Collections.Concurrent;
using Evento.Internals;
using Evento.Repositories.Subscription;
using Evento.Services.PubSub;
using Evento.Services.Transport;

namespace Evento.Services.SubscriptionRegistry;

public class SubscriptionRegistry : ISubscriptionRegistry
{
    private readonly ConcurrentDictionary<string, IConsumer> consumerPerSubscription = new();

    private readonly AsyncLock mutex = new();

    private readonly IEventPubSub eventPubSub;
    private readonly IEventTransport eventTransport;

    public SubscriptionRegistry(IEventPubSub eventPubSub, IEventTransport eventTransport)
    {
        this.eventPubSub = eventPubSub;
        this.eventTransport = eventTransport;
    }

    public IReadOnlySet<string> Registered => consumerPerSubscription.Select(x => x.Key).ToHashSet();

    public async Task RegisterAsync(Subscription subscription, CancellationToken cancellationToken = default)
    {
        using var _ = await mutex.AcquireAsync(cancellationToken);

        if (consumerPerSubscription.ContainsKey(subscription.Id)) return;

        var consumer = await eventPubSub.SubscribeAsync(
            subscription,
            async (s, e, c) => await eventTransport.TransmitAsync(s.Endpoint, e, c),
            cancellationToken
        );
        consumerPerSubscription[subscription.Id] = consumer;
    }

    public async Task<bool> UnregisterAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        using var _ = await mutex.AcquireAsync(cancellationToken);

        if (!consumerPerSubscription.TryGetValue(subscriptionId, out var consumer)) return true;

        var wasShutdown = await consumer.ShutdownAsync(cancellationToken);
        if (!wasShutdown) return false;

        consumerPerSubscription.TryRemove(subscriptionId, out var _);
        return true;
    }
}