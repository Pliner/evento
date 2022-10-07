using Evento.Services;
using Medallion.Threading;

namespace Evento.HostedServices;

public class SubscriptionManagerService : BackgroundService
{
    private readonly ILogger<SubscriptionManagerService> logger;
    private readonly ISubscriptionManager subscriptionManager;
    private readonly IDistributedLockProvider distributedLockProvider;

    public SubscriptionManagerService(
        ILogger<SubscriptionManagerService> logger,
        ISubscriptionManager subscriptionManager,
        IDistributedLockProvider distributedLockProvider
    )
    {
        this.logger = logger;
        this.subscriptionManager = subscriptionManager;
        this.distributedLockProvider = distributedLockProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var distributedLock = distributedLockProvider.CreateLock("subscription-manager");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var handle = await distributedLock.AcquireAsync(null, stoppingToken);

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, handle.HandleLostToken);

                await subscriptionManager.RunAsync(cts.Token);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(exception, "Unexpected exception");

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}