using Evento.Repositories.Subscription;
using Evento.Services.SubscriptionRegistry;

namespace Evento.Services;

public class ActiveSubscriptionsManager : BackgroundService
{
    private readonly ILogger<ActiveSubscriptionsManager> logger;
    private readonly ISubscriptionRepository subscriptionRepository;
    private readonly ISubscriptionRegistry subscriptionRegistry;

    public ActiveSubscriptionsManager(
        ILogger<ActiveSubscriptionsManager> logger,
        ISubscriptionRepository subscriptionRepository,
        ISubscriptionRegistry subscriptionRegistry
    )
    {
        this.logger = logger;
        this.subscriptionRepository = subscriptionRepository;
        this.subscriptionRegistry = subscriptionRegistry;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var activeSubscriptions = await subscriptionRepository.SelectActiveAsync(stoppingToken);
                var staleSubscriptionsIds = new HashSet<string>(subscriptionRegistry.Registered);

                foreach (var activeSubscription in activeSubscriptions)
                {
                    staleSubscriptionsIds.Remove(activeSubscription.Id);
                    await subscriptionRegistry.RegisterAsync(activeSubscription, stoppingToken);
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
                    var wasUnregistered = await subscriptionRegistry.UnregisterAsync(staleSubscriptionId, stoppingToken);
                    if (!wasUnregistered) continue;

                    await subscriptionRepository.DeactivateAsync(staleSubscriptionId, stoppingToken);
                }
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to manage active subscriptions");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
