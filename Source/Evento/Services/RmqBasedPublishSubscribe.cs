using System.Collections.Concurrent;
using EasyNetQ;
using EasyNetQ.Topology;
using Evento.Internals;
using Evento.Repositories.Subscription;

namespace Evento.Services;

public sealed record RmqBasedTransportOptions(string ExchangeName = "events");

public sealed class RmqBasedPublishSubscribe : IPublishSubscribe
{
    private const int MaxRetryExchanges = 16; // 2**16 ~= 9 hours

    private readonly IAdvancedBus bus;

    private readonly ConcurrentDictionary<string, IDisposable> consumerPerSubscription = new();
    private readonly ConcurrentDictionary<string, Subscription> subscriptions = new();
    private readonly AsyncLock mutex = new();
    private readonly AsyncLazy<Exchange> lazyExchange;
    private readonly AsyncLazy<IReadOnlyDictionary<int, Exchange>> lazyRetryExchanges;

    public RmqBasedPublishSubscribe(IAdvancedBus bus, RmqBasedTransportOptions options)
    {
        this.bus = bus;

        lazyExchange = new AsyncLazy<Exchange>(
            async c =>
            {
                if (options.ExchangeName == "") return Exchange.Default;

                return await bus.ExchangeDeclareAsync(
                    options.ExchangeName, x => x.WithType(ExchangeType.Topic).AsDurable(true), c
                );
            });
        lazyRetryExchanges = new AsyncLazy<IReadOnlyDictionary<int, Exchange>>(
            async ct =>
            {
                var retryExchanges = new Dictionary<int, Exchange>(MaxRetryExchanges);

                for (var attempt = 1; attempt <= MaxRetryExchanges; ++attempt)
                {
                    var retryDelaySeconds = (int)Math.Pow(2, attempt - 1);
                    var retryExchange = await bus.ExchangeDeclareAsync(
                        $"evento:retry-after-{retryDelaySeconds}s",
                        c => c.WithType(ExchangeType.Fanout).AsDurable(true),
                        ct
                    );
                    var retryQueue = await bus.QueueDeclareAsync(
                        $"evento:retry-after-{retryDelaySeconds}s",
                        c => c.WithQueueType(QueueType.Quorum)
                            .WithDeadLetterExchange(Exchange.Default)
                            .WithMessageTtl(TimeSpan.FromSeconds(retryDelaySeconds))
                            .WithOverflowType(OverflowType.RejectPublish)
                            .WithDeadLetterStrategy(DeadLetterStrategy.AtLeastOnce)
                            .AsDurable(true),
                        ct
                    );
                    await bus.BindAsync(retryExchange, retryQueue, "", ct);
                    retryExchanges.Add(attempt, retryExchange);
                }

                return retryExchanges;
            }
        );
    }

    public async Task PublishAsync(EventProperties properties, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        var exchange = await lazyExchange.GetAsync(cancellationToken);
        var messageProperties = CreateMessageProperties(properties);
        await bus.PublishAsync(exchange, properties.Type, false, messageProperties, payload, cancellationToken);
    }

    public IReadOnlySet<string> ActiveSubscriptions => consumerPerSubscription.Keys.ToHashSet();

    public async Task StartSubscriptionAsync(
        Subscription subscription, EventHandlerDelegate eventHandlerDelegate, CancellationToken cancellationToken = default
    )
    {
        if (consumerPerSubscription.ContainsKey(subscription.Name))
            throw new InvalidOperationException($"Subscription {subscription.Name} has already made");

        using var _ = await mutex.AcquireAsync(cancellationToken);

        if (consumerPerSubscription.ContainsKey(subscription.Name))
            throw new InvalidOperationException($"Subscription {subscription.Name} has already made");

        var exchange = await lazyExchange.GetAsync(cancellationToken).ConfigureAwait(false);
        var queue = await bus.QueueDeclareAsync(
            subscription.GetQueueName(),
            x => x
                .WithQueueType(QueueType.Quorum)
                .WithOverflowType(OverflowType.RejectPublish)
                .AsDurable(true),
            cancellationToken
        );
        var failedQueue = await bus.QueueDeclareAsync(
            subscription.GetFailedQueueName(),
            x => x
                .WithQueueType(QueueType.Quorum)
                .WithOverflowType(OverflowType.RejectPublish)
                .AsDurable(true),
            cancellationToken
        );

        foreach (var type in subscription.Types)
            await bus.QueueBindAsync(queue.Name, exchange.Name, type, null, cancellationToken);

        foreach (var type in subscription.DeletedTypes)
            await bus.QueueUnbindAsync(queue.Name, exchange.Name, type, null, cancellationToken);

        subscriptions[subscription.Name] = subscription;
        var consumer = bus.Consume(
            queue, GetMessageHandlerFunc(subscription.Name, eventHandlerDelegate, queue, failedQueue)
        );
        consumerPerSubscription[subscription.Name] = consumer;
    }

    public async Task RefreshSubscriptionAsync(Subscription subscription, CancellationToken cancellationToken = default)
    {
        if (!subscription.Active) throw new ArgumentOutOfRangeException(nameof(subscription), subscription, null);

        if (!subscriptions.TryGetValue(subscription.Name, out var existentSubscription)) return;
        if (existentSubscription.Version >= subscription.Version) return;

        using var _ = await mutex.AcquireAsync(cancellationToken);

        if (!subscriptions.TryGetValue(subscription.Name, out existentSubscription)) return;
        if (existentSubscription.Version >= subscription.Version) return;

        var exchange = await lazyExchange.GetAsync(cancellationToken);

        foreach (var type in subscription.Types)
            await bus.QueueBindAsync(subscription.GetQueueName(), exchange.Name, type, null, cancellationToken);

        foreach (var type in subscription.DeletedTypes)
            await bus.QueueUnbindAsync(subscription.GetQueueName(), exchange.Name, type, null, cancellationToken);

        subscriptions[subscription.Name] = subscription;
    }

    public async Task DeactivateSubscriptionAsync(Subscription subscription, CancellationToken cancellationToken = default)
    {
        if (subscription.Active) throw new ArgumentOutOfRangeException(nameof(subscription), subscription, null);

        if (subscriptions.TryGetValue(subscription.Name, out var existentSubscription) && existentSubscription.Version >= subscription.Version)
            return;

        using var _ = await mutex.AcquireAsync(cancellationToken);

        if (subscriptions.TryGetValue(subscription.Name, out existentSubscription) && existentSubscription.Version >= subscription.Version)
            return;

        var exchange = await lazyExchange.GetAsync(cancellationToken);
        foreach (var type in subscription.DeletedTypes)
            await bus.QueueUnbindAsync(subscription.GetQueueName(), exchange.Name, type, null, cancellationToken);

        if (consumerPerSubscription.TryGetValue(subscription.Name, out var consumer))
        {
            consumer.Dispose();
            consumerPerSubscription.TryRemove(subscription.Name, out consumer);
        }

        subscriptions[subscription.Name] = subscription;
    }

    public void Dispose()
    {
        subscriptions.Clear();
        consumerPerSubscription.ClearAndDispose();
        mutex.Dispose();
        lazyRetryExchanges.Dispose();
        lazyExchange.Dispose();
    }

    private Func<ReadOnlyMemory<byte>, MessageProperties, MessageReceivedInfo, CancellationToken, Task> GetMessageHandlerFunc(
        string subscriptionName, EventHandlerDelegate eventHandlerDelegate, Queue queue, Queue failedQueue
    )
    {
        return async (payload, properties, receivedInfo, cancellationToken) =>
        {
            var subscription = subscriptions[subscriptionName];
            var eventProperties = new EventProperties(
                Type: properties.Type ?? receivedInfo.RoutingKey,
                ContentType: properties.ContentType ?? "application/json"
            );
            var verdict = await eventHandlerDelegate(subscription, eventProperties, payload, cancellationToken);
            if (verdict == EventHandlerResult.Processed) return;

            var nextRetryAttempt = properties.Headers.TryGetValue("Evento-Retry-Attempt", out var retryAttempt)
                ? (int)(retryAttempt ?? 0) + 1
                : 1;

            var retryExchanges = await lazyRetryExchanges.GetAsync(cancellationToken);
            if (retryExchanges.TryGetValue(nextRetryAttempt, out var existingRetryExchange))
            {
                await bus.PublishAsync(
                    existingRetryExchange,
                    queue.Name,
                    true,
                    CreateMessageProperties(eventProperties, nextRetryAttempt),
                    payload,
                    cancellationToken
                );
            }
            else
            {
                await bus.PublishAsync(
                    Exchange.Default,
                    failedQueue.Name,
                    true,
                    CreateMessageProperties(eventProperties, nextRetryAttempt),
                    payload,
                    cancellationToken
                );
            }
        };
    }

    private static MessageProperties CreateMessageProperties(in EventProperties properties, int? retryAttempt = null)
    {
        var messageProperties = new MessageProperties
        {
            Type = properties.Type,
            ContentType = properties.ContentType,
            DeliveryMode = MessageDeliveryMode.Persistent,
            AppId = "evento",
        };
        if (retryAttempt != null)
            messageProperties.Headers.Add("Evento-Retry-Attempt", retryAttempt);
        return messageProperties;
    }
}