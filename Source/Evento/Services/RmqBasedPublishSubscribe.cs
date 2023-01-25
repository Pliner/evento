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
    private readonly Func<Subscription, AsyncLazy<(Queue Queue, Queue FailedQueue)>> lazyQueueFunc;
    private readonly ConcurrentDictionary<Subscription, AsyncLazy<(Queue Queue, Queue FailedQueue)>> lazyQueues;

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
        lazyQueues = new ConcurrentDictionary<Subscription, AsyncLazy<(Queue Queue, Queue FailedQueue)>>();
        lazyQueueFunc = s => new AsyncLazy<(Queue Queue, Queue FailedQueue)>(async ct =>
        {
            var queue = await bus.QueueDeclareAsync(
                s.GetQueueName(),
                x => x.WithQueueType(QueueType.Quorum).WithOverflowType(OverflowType.RejectPublish).AsDurable(true),
                ct
            );
            var failedQueue = await bus.QueueDeclareAsync(
                s.GetFailedQueueName(),
                x => x.WithQueueType(QueueType.Quorum).WithOverflowType(OverflowType.RejectPublish).AsDurable(true),
                ct
            );
            return (queue, failedQueue);
        });
    }

    public async Task PublishAsync(EventProperties properties, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        var exchange = await lazyExchange.GetAsync(cancellationToken);
        var messageProperties = CreateMessageProperties(properties);
        await bus.PublishAsync(exchange, properties.Type, false, messageProperties, payload, cancellationToken);
    }

    public async Task MaintainSubscriptionAsync(
        Subscription subscription, EventHandlerDelegate eventHandlerDelegate, CancellationToken cancellationToken = default
    )
    {
        if (subscriptions.TryGetValue(subscription.Name, out var existentSubscription) && existentSubscription.Version >= subscription.Version)
            return;

        using var _ = await mutex.AcquireAsync(cancellationToken);

        if (subscriptions.TryGetValue(subscription.Name, out existentSubscription) && existentSubscription.Version >= subscription.Version)
            return;

        var exchange = await lazyExchange.GetAsync(cancellationToken).ConfigureAwait(false);

        if (subscription.Active)
        {
            var (queue, failedQueue) = await lazyQueues.GetOrAdd(subscription, lazyQueueFunc).GetAsync(cancellationToken);

            foreach (var type in subscription.Types)
                await bus.QueueBindAsync(subscription.GetQueueName(), exchange.Name, type, null, cancellationToken);

            foreach (var type in subscription.DeletedTypes)
                await bus.QueueUnbindAsync(subscription.GetQueueName(), exchange.Name, type, null, cancellationToken);

            if (!consumerPerSubscription.ContainsKey(subscription.Name))
            {
                var consumer = bus.Consume(queue, GetMessageHandlerFunc(subscription.Name, eventHandlerDelegate, queue, failedQueue));
                consumerPerSubscription[subscription.Name] = consumer;
            }
        }
        else
        {
            foreach (var type in subscription.DeletedTypes.Concat(subscription.Types))
                await bus.QueueUnbindAsync(subscription.GetQueueName(), exchange.Name, type, null, cancellationToken);

            if (consumerPerSubscription.TryGetValue(subscription.Name, out var consumer))
            {
                consumer.Dispose();
                consumerPerSubscription.TryRemove(subscription.Name, out var _);
            }
        }

        subscriptions[subscription.Name] = subscription;
    }

    public async Task InterruptSubscriptionsAsync(CancellationToken cancellationToken = default)
    {
        using var _ = await mutex.AcquireAsync(cancellationToken);

        consumerPerSubscription.ClearAndDispose();
        subscriptions.Clear();
    }

    public void Dispose()
    {
        consumerPerSubscription.ClearAndDispose();
        subscriptions.Clear();
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

            var nextRetryAttempt = properties.Headers != null && properties.Headers.TryGetValue("Evento-Retry-Attempt", out var retryAttempt)
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
            messageProperties = messageProperties.SetHeader("Evento-Retry-Attempt", retryAttempt);
        return messageProperties;
    }
}