using System.Collections.Concurrent;
using EasyNetQ;
using EasyNetQ.Consumer;
using EasyNetQ.Topology;
using Evento.Internals;
using Evento.Repositories.Subscription;

namespace Evento.Services;

public class RmqBasedTransport : IPublishSubscribeTransport
{
    private const string ExchangeName = "events";

    private readonly IAdvancedBus bus;
    private readonly IDirectTransport transport;

    private readonly ConcurrentDictionary<string, Exchange> exchanges = new();

    private readonly ConcurrentDictionary<Guid, Consumer> consumerPerSubscription = new();
    private readonly AsyncLock mutex = new();

    public RmqBasedTransport(IAdvancedBus bus, IDirectTransport transport)
    {
        this.bus = bus;
        this.transport = transport;
    }

    public async Task PublishAsync(Event @event, CancellationToken cancellationToken = default)
    {
        var exchange = await EnsureExchangeDeclaredAsync(ExchangeName, cancellationToken);
        var properties = new MessageProperties
        {
            Type = @event.Type,
            ContentType = "application/octet-stream",
            DeliveryMode = MessageDeliveryMode.Persistent
        };
        await bus.PublishAsync(exchange, @event.Type, true, properties, @event.Payload, cancellationToken);
    }

    public IReadOnlySet<Guid> ActiveSubscriptions => consumerPerSubscription.Keys.ToHashSet();

    public async Task SubscribeAsync(Subscription subscription, CancellationToken cancellationToken = default)
    {
        if (consumerPerSubscription.ContainsKey(subscription.Id))
            return;

        using var _ = await mutex.AcquireAsync(cancellationToken);

        if (consumerPerSubscription.ContainsKey(subscription.Id))
            return;

        var exchange = await EnsureExchangeDeclaredAsync(ExchangeName, cancellationToken);
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
            async (b, p, _, c) =>
            {
                await transport.SendAsync(subscription, new Event(p.Type, b), c);
                return AckStrategies.Ack;
            },
            _ => { }
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

        consumerPerSubscription.Remove(subscriptionId);
        return true;
    }

    public void Dispose()
    {
        exchanges.Clear();
        consumerPerSubscription.ClearAndDispose();
    }

    private async Task<Exchange> EnsureExchangeDeclaredAsync(string exchangeName, CancellationToken cancellationToken)
    {
        if (exchanges.TryGetValue(exchangeName, out var exchange))
            return exchange;

        exchange = await bus.ExchangeDeclareAsync(exchangeName, x => x.WithType(ExchangeType.Topic), cancellationToken);
        exchanges.TryAdd(exchangeName, exchange);
        return exchange;
    }

    private class Consumer : IDisposable
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