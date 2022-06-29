using Evento.Infrastructure;
using Evento.Repositories.Subscription;
using Evento.Services.SubscriptionRegistry;

namespace Evento.Services;

public class ActiveSubscriptionsManager : IPeriodicJob
{
    private readonly ISubscriptionRepository subscriptionRepository;
    private readonly ISubscriptionRegistry subscriptionRegistry;

    public ActiveSubscriptionsManager(
        ISubscriptionRepository subscriptionRepository,
        ISubscriptionRegistry subscriptionRegistry
    )
    {
        this.subscriptionRepository = subscriptionRepository;
        this.subscriptionRegistry = subscriptionRegistry;
    }

    public string Name => "ActiveSubscriptionsManager";
    public TimeSpan Interval => TimeSpan.FromSeconds(5);

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var activeSubscriptions = await subscriptionRepository.SelectActiveAsync(cancellationToken);
        var staleSubscriptionsIds = subscriptionRegistry.Registered.ToHashSet();

        foreach (var activeSubscription in activeSubscriptions)
        {
            staleSubscriptionsIds.Remove(activeSubscription.Id);
            await subscriptionRegistry.RegisterAsync(activeSubscription, cancellationToken);
        }

        var notLastActiveSubscription = activeSubscriptions
            .GroupBy(
                x => x.Name,
                x => x,
                (_, g) => g.OrderByDescending(x => x.Version)
                    .Select(x => x.Id)
                    .Skip(1)
            )
            .SelectMany(x => x);

        foreach (var staleSubscriptionId in staleSubscriptionsIds.Concat(notLastActiveSubscription))
        {
            var wasUnregistered = await subscriptionRegistry.UnregisterAsync(staleSubscriptionId, cancellationToken);
            if (!wasUnregistered) continue;

            await subscriptionRepository.DeactivateAsync(staleSubscriptionId, cancellationToken);
        }
    }
}