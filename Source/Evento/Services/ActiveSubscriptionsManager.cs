using Evento.Infrastructure;
using Evento.Repositories.Subscription;
using Prometheus;

namespace Evento.Services;

public sealed class ActiveSubscriptionsManager : IPeriodicJob
{
    private readonly ILogger<ActiveSubscriptionsManager> logger;
    private readonly ISubscriptionRepository subscriptionRepository;
    private readonly IDirectTransport transport;
    private readonly IPublishSubscribe publishSubscribe;
    private readonly Counter failedEventsCounter;
    private readonly Counter totalEventsCounter;

    public ActiveSubscriptionsManager(
        ILogger<ActiveSubscriptionsManager> logger,
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

    public string Name => "ActiveSubscriptionsManager";

    public TimeSpan Interval => TimeSpan.FromSeconds(5);

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var subscriptionsNames = await subscriptionRepository.SelectNamesAsync(cancellationToken);
        var activeSubscriptions = publishSubscribe.ActiveSubscriptions;

        foreach (var subscriptionName in subscriptionsNames)
        {
            var subscription = await subscriptionRepository.TryGetByNameAsync(subscriptionName, cancellationToken);
            if (subscription == null) continue;

            if (subscription.Active)
            {
                if (activeSubscriptions.Contains(subscription.Name))
                    await publishSubscribe.RefreshSubscriptionAsync(subscription, cancellationToken);
                else
                    await publishSubscribe.StartSubscriptionAsync(subscription, HandleEventAsync, cancellationToken);
            }
            else
            {
                await publishSubscribe.DeactivateSubscriptionAsync(subscription, cancellationToken);
            }
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