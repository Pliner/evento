using System.Collections.Concurrent;
using EasyNetQ;
using EasyNetQ.Consumer;
using EasyNetQ.Topology;
using Evento.Repositories.Subscription;


namespace Evento.Services.PubSub;

public class RmqBasedPubSub : IEventPubSub
{
    private const string ExchangeName = "events";

    private readonly IAdvancedBus bus;

    private readonly ConcurrentDictionary<string, Exchange> exchanges = new();
    private readonly ConcurrentQueue<IDisposable> consumers = new();

    public RmqBasedPubSub(IAdvancedBus bus) => this.bus = bus;

    public async Task PublishAsync(Event @event, CancellationToken cancellationToken = default)
    {
        var exchange = await EnsureExchangeDeclaredAsync(ExchangeName, cancellationToken);
        var properties = new MessageProperties
        {
            MessageId = @event.Id,
            Type = @event.Type,
            Timestamp = @event.Timestamp.Ticks,
            ContentType = "application/octet-stream",
            DeliveryMode = MessageDeliveryMode.Persistent
        };
        await bus.PublishAsync(exchange, @event.Type, true, properties, @event.Payload, cancellationToken);
    }

    private async Task<Exchange> EnsureExchangeDeclaredAsync(string exchangeName, CancellationToken cancellationToken)
    {
        if (exchanges.TryGetValue(exchangeName, out var exchange))
            return exchange;

        exchange = await bus.ExchangeDeclareAsync(exchangeName, x => x.WithType(ExchangeType.Topic), cancellationToken);
        exchanges.TryAdd(exchangeName, exchange);
        return exchange;
    }

    public async Task<IConsumer> SubscribeAsync(
        Subscription subscription,
        Func<Subscription, Event, CancellationToken, Task> handler,
        CancellationToken cancellationToken
    )
    {
        var exchange = await EnsureExchangeDeclaredAsync(ExchangeName, cancellationToken);
        var queue = await bus.QueueDeclareAsync(
            $"{subscription.Name}:{subscription.Version}",
            x => x.WithQueueType(QueueType.Quorum)
                .WithOverflowType(OverflowType.RejectPublish)
                .WithSingleActiveConsumer(),
            cancellationToken
        );
        var bindings = new List<Binding<Queue>>();
        foreach (var type in subscription.Types)
            bindings.Add(await bus.BindAsync(exchange, queue, type, cancellationToken));

        var consumer = bus.Consume(
            queue,
            async (b, p, _, c) =>
            {
                var @event = new Event(p.MessageId, p.Type, new DateTime(p.Timestamp, DateTimeKind.Utc), b);
                await handler(subscription, @event, c);
                return AckStrategies.Ack;
            },
            _ => { }
        );
        consumers.Enqueue(consumer);
        return new Consumer(bus, queue, bindings, consumer);
    }

    private class Consumer : IConsumer
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
    }

    public void Dispose()
    {
        foreach (var consumer in consumers)
            consumer.Dispose();
    }
}