using Evento.Infrastructure;
using Evento.Repositories.Subscription;

namespace Evento.Services;

public class ActiveSubscriptionsManager : IPeriodicJob
{
    private readonly ISubscriptionRepository subscriptionRepository;
    private readonly IPublishSubscribeTransport transport;

    public ActiveSubscriptionsManager(
        ISubscriptionRepository subscriptionRepository, IPublishSubscribeTransport transport
    )
    {
        this.subscriptionRepository = subscriptionRepository;
        this.transport = transport;
    }

    public string Name => "ActiveSubscriptionsManager";
    public TimeSpan Interval => TimeSpan.FromSeconds(5);

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var activeSubscriptions = await subscriptionRepository.SelectActiveAsync(cancellationToken);
        var staleSubscriptionsIds = transport.ActiveSubscriptions.ToHashSet();

        foreach (var activeSubscription in activeSubscriptions)
        {
            staleSubscriptionsIds.Remove(activeSubscription.Id);
            await transport.SubscribeAsync(activeSubscription, cancellationToken);
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
            var wasUnregistered = await transport.UnsubscribeAsync(staleSubscriptionId, cancellationToken);
            if (!wasUnregistered) continue;

            await subscriptionRepository.DeactivateAsync(staleSubscriptionId, cancellationToken);
        }
    }
}