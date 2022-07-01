using Evento.Infrastructure;
using Evento.Repositories.Subscription;
using Prometheus;

namespace Evento.Services;

public class ActiveSubscriptionsManager : IPeriodicJob
{
    private readonly ILogger<ActiveSubscriptionsManager> logger;
    private readonly ISubscriptionRepository subscriptionRepository;
    private readonly IDirectTransport transport;
    private readonly IPublishSubscribeTransport publishSubscribeTransport;
    private readonly Counter failedEventsCounter;
    private readonly Counter totalEventsCounter;

    public ActiveSubscriptionsManager(
        ILogger<ActiveSubscriptionsManager> logger,
        ISubscriptionRepository subscriptionRepository,
        IDirectTransport transport,
        IPublishSubscribeTransport publishSubscribeTransport,
        IMetricFactory metricsFactory
    )
    {
        this.logger = logger;
        this.subscriptionRepository = subscriptionRepository;
        this.transport = transport;
        this.publishSubscribeTransport = publishSubscribeTransport;

        failedEventsCounter = metricsFactory.CreateCounter(
            "evento_events_failed",
            "Count of events that haven't been sent successfully by Evento.",
            new CounterConfiguration { LabelNames = new[] { "event_type", "subscription_name" } }
        );
        totalEventsCounter = metricsFactory.CreateCounter(
            "evento_events_total",
            "Count of events that have been sent by Evento.",
            new CounterConfiguration { LabelNames = new[] { "event_type", "subscription_name" } }
        );
    }

    public string Name => "ActiveSubscriptionsManager";
    public TimeSpan Interval => TimeSpan.FromSeconds(5);

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var activeSubscriptions = await subscriptionRepository.SelectActiveAsync(cancellationToken);
        var staleSubscriptionsIds = publishSubscribeTransport.ActiveSubscriptions.ToHashSet();

        foreach (var activeSubscription in activeSubscriptions)
        {
            staleSubscriptionsIds.Remove(activeSubscription.Id);
            await publishSubscribeTransport.SubscribeAsync(
                activeSubscription,
                async (s, e, c) =>
                {
                    try
                    {
                        await transport.SendAsync(s, e, c);
                    }
                    catch (OperationCanceledException) when (c.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        logger.LogError(exception, "Failed to deliver {EventType} to {SubscriptionName}", e.Type, s.Id);

                        failedEventsCounter.Labels(e.Type, s.Name).Inc();
                    }
                    finally
                    {
                        totalEventsCounter.Labels(e.Type, s.Name).Inc();
                    }
                },
                cancellationToken
            );
        }

        var notLastActiveSubscription = activeSubscriptions
            .GroupBy(
                x => x.Name,
                x => x,
                (_, g) => g.OrderByDescending(x => x.Version).Select(x => x.Id).Skip(1)
            )
            .SelectMany(x => x);

        foreach (var staleSubscriptionId in staleSubscriptionsIds.Concat(notLastActiveSubscription))
        {
            var wasUnregistered = await publishSubscribeTransport.UnsubscribeAsync(staleSubscriptionId, cancellationToken);
            if (!wasUnregistered) continue;

            await subscriptionRepository.DeactivateAsync(staleSubscriptionId, cancellationToken);
        }
    }
}