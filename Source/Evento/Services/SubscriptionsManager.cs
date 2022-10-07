using Evento.Internals;
using Evento.Repositories.Subscription;
using Prometheus;

namespace Evento.Services;

public interface ISubscriptionsManager
{
    Task RunAsync(CancellationToken cancellationToken = default);
}

public sealed class SubscriptionsManager : ISubscriptionsManager
{
    private readonly ILogger<SubscriptionsManager> logger;
    private readonly ISubscriptionRepository subscriptionRepository;
    private readonly IDirectTransport transport;
    private readonly IPublishSubscribe publishSubscribe;
    private readonly Counter failedEventsCounter;
    private readonly Counter totalEventsCounter;

    public SubscriptionsManager(
        ILogger<SubscriptionsManager> logger,
        ISubscriptionRepository subscriptionRepository,
        IDirectTransport transport,
        IPublishSubscribe publishSubscribe,
        IMetricFactory metricsFactory
    )
    {
        this.logger = logger;
        this.subscriptionRepository = subscriptionRepository;
        this.transport = transport;
        this.publishSubscribe = publishSubscribe;

        failedEventsCounter = metricsFactory.CreateCounter(
            "evento_events_sent_failures",
            "Count of events that haven't been sent successfully by Evento.",
            new CounterConfiguration { LabelNames = new[] { "event_type", "subscription_name" } }
        );
        totalEventsCounter = metricsFactory.CreateCounter(
            "evento_events_sent_total",
            "Count of events that have been sent by Evento.",
            new CounterConfiguration { LabelNames = new[] { "event_type", "subscription_name" } }
        );
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        // ReSharper disable once MethodSupportsCancellation
        await using var _ = AsyncDisposableActions.Create(() => publishSubscribe.InterruptSubscriptionsAsync());

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var subscriptionsNames = await subscriptionRepository.SelectNamesAsync(cancellationToken);

                foreach (var subscriptionName in subscriptionsNames)
                {
                    var subscription = await subscriptionRepository.TryGetByNameAsync(subscriptionName, cancellationToken);
                    if (subscription == null) continue;

                    await publishSubscribe.MaintainSubscriptionAsync(subscription, HandleEventAsync, cancellationToken);
                }
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogError(exception, "Failed to maintain subscriptions");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        }
    }

    private async Task<EventHandlerResult> HandleEventAsync(
        Subscription subscription, EventProperties properties, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken
    )
    {
        try
        {
            await transport.SendAsync(subscription, properties, payload, cancellationToken);
            return EventHandlerResult.Processed;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to deliver event {EventType} to subscription {SubscriptionName}", properties.Type, subscription.Name);

            failedEventsCounter.Labels(properties.Type, subscription.Name).Inc();
        }
        finally
        {
            totalEventsCounter.Labels(properties.Type, subscription.Name).Inc();
        }

        return EventHandlerResult.Failed;
    }
}