using Evento.Services;
using Medallion.Threading;

namespace Evento.HostedServices;

public class SubscriptionsManagerService : BackgroundService
{
    private readonly ILogger<SubscriptionsManagerService> logger;
    private readonly ISubscriptionsManager subscriptionsManager;
    private readonly IDistributedLockProvider distributedLockProvider;

    public SubscriptionsManagerService(
        ILogger<SubscriptionsManagerService> logger,
        ISubscriptionsManager subscriptionsManager,
        IDistributedLockProvider distributedLockProvider
    )
    {
        this.logger = logger;
        this.subscriptionsManager = subscriptionsManager;
        this.distributedLockProvider = distributedLockProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var distributedLock = distributedLockProvider.CreateLock("subscriptions-manager");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var handle = await distributedLock.AcquireAsync(null, stoppingToken);

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, handle.HandleLostToken);

                await subscriptionsManager.RunAsync(cts.Token);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(exception, "Unexpected exception");

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}