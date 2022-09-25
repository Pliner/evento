using System.Collections.Concurrent;
using EasyNetQ;
using EasyNetQ.Consumer;
using EasyNetQ.Topology;
using Evento.Internals;
using Evento.Repositories.Subscription;

namespace Evento.Services;

public sealed record RmqBasedTransportOptions(string ExchangeName = "events");

public sealed class RmqBasedTransport : IPublishSubscribeTransport
{
    private readonly IAdvancedBus bus;

    private readonly ConcurrentDictionary<Guid, Consumer> consumerPerSubscription = new();
    private readonly AsyncLock mutex = new();
    private readonly AsyncLazy<Exchange> lazyExchange;

    public RmqBasedTransport(IAdvancedBus bus, RmqBasedTransportOptions options)
    {
        this.bus = bus;

        lazyExchange = new AsyncLazy<Exchange>(
            c => bus.ExchangeDeclareAsync(
                options.ExchangeName, x => x.WithType(ExchangeType.Topic).AsDurable(true), c
            )
        );
    }

    public async Task PublishAsync(Event @event, CancellationToken cancellationToken = default)
    {
        var exchange = await lazyExchange.GetAsync(cancellationToken).ConfigureAwait(false);
        var properties = new MessageProperties
        {
            Type = @event.Type,
            ContentType = "application/octet-stream",
            DeliveryMode = MessageDeliveryMode.Persistent
        };
        await bus.PublishAsync(exchange, @event.Type, true, properties, @event.Payload, cancellationToken);
    }

    public IReadOnlySet<Guid> ActiveSubscriptions => consumerPerSubscription.Keys.ToHashSet();

    public async Task SubscribeAsync(
        Subscription subscription,
        Func<Subscription, Event, CancellationToken, Task> transportFunc,
        CancellationToken cancellationToken = default
    )
    {
        if (consumerPerSubscription.ContainsKey(subscription.Id))
            return;

        using var _ = await mutex.AcquireAsync(cancellationToken);

        if (consumerPerSubscription.ContainsKey(subscription.Id))
            return;

        var exchange = await lazyExchange.GetAsync(cancellationToken).ConfigureAwait(false);
        var queue = await bus.QueueDeclareAsync(
            $"{subscription.Name}:{subscription.Version}",
            x => x.WithQueueType(QueueType.Quorum)
                .WithOverflowType(OverflowType.RejectPublish)
                .WithSingleActiveConsumer(),
            cancellationToken
        );
        var bindings = new List<Binding<Queue>>(subscription.Types.Length);
        foreach (var type in subscription.Types)
            bindings.Add(await bus.BindAsync(exchange, queue, type, cancellationToken));

        var consumer = bus.Consume(
            queue,
            async (b, p, ri, c) =>
            {
                await transportFunc(subscription, new Event(p.Type ?? ri.RoutingKey, b), c);
                return AckStrategies.Ack;
            },
            _ => {}
        );

        if (consumerPerSubscription.TryAdd(subscription.Id, new Consumer(bus, queue, bindings, consumer)))
            return;

        consumer.Dispose();
        throw new Exception($"Subscription {subscription.Id} has already made");
    }

    public async Task<bool> UnsubscribeAsync(Guid subscriptionId, CancellationToken cancellationToken = default)
    {
        if (!consumerPerSubscription.ContainsKey(subscriptionId))
            return true;

        using var _ = await mutex.AcquireAsync(cancellationToken);

        if (!consumerPerSubscription.TryGetValue(subscriptionId, out var consumer))
            return true;

        var wasShutdown = await consumer.ShutdownAsync(cancellationToken);
        if (!wasShutdown) return false;

        consumerPerSubscription.TryRemove(subscriptionId, out var _);
        return true;
    }

    public void Dispose()
    {
        consumerPerSubscription.ClearAndDispose();
        mutex.Dispose();
        lazyExchange.Dispose();
    }

    private sealed class Consumer : IDisposable
    {
        private readonly IAdvancedBus bus;
        private readonly Queue queue;
        private readonly IReadOnlyList<Binding<Queue>> bindings;
        private readonly IDisposable consumer;

        public Consumer(
            IAdvancedBus bus,
            Queue queue,
            IReadOnlyList<Binding<Queue>> bindings,
            IDisposable consumer
        )
        {
            this.bus = bus;
            this.queue = queue;
            this.bindings = bindings;
            this.consumer = consumer;
        }

        public async Task<bool> ShutdownAsync(CancellationToken cancellationToken)
        {
            foreach (var binding in bindings)
                await bus.UnbindAsync(binding, cancellationToken);

            var stats = await bus.GetQueueStatsAsync(queue.Name, cancellationToken);
            if (stats.MessagesCount > 0) return false;

            consumer.Dispose();
            return true;
        }

        public void Dispose() => consumer.Dispose();
    }
}