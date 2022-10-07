using Evento.Internals;
using Evento.Services;
using Medallion.Threading;
using Prometheus;

namespace Evento.HostedServices;

public sealed class SubscriptionManagerService : BackgroundService
{
    private static readonly TimeSpan RetryOnFailureDelay = TimeSpan.FromSeconds(5);

    private readonly ILogger<SubscriptionManagerService> logger;
    private readonly ISubscriptionManager subscriptionManager;
    private readonly IDistributedLockProvider distributedLockProvider;
    private readonly Gauge acquiredLockGauge;

    public SubscriptionManagerService(
        ILogger<SubscriptionManagerService> logger,
        ISubscriptionManager subscriptionManager,
        IDistributedLockProvider distributedLockProvider,
        IMetricFactory metricFactory
    )
    {
        this.logger = logger;
        this.subscriptionManager = subscriptionManager;
        this.distributedLockProvider = distributedLockProvider;
        acquiredLockGauge = metricFactory.CreateGauge(
            "evento_acquired_lock",
            "Acquired locks by evento",
            new GaugeConfiguration { LabelNames = new[] { "lock_name" } }
        );
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var distributedLock = distributedLockProvider.CreateLock("subscription-manager");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var handle = await distributedLock.AcquireAsync(null, stoppingToken);
                using var _ = acquiredLockGauge.Labels("subscription-manager").TrackInProgress();

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, handle.HandleLostToken);

                await subscriptionManager.RunAsync(cts.Token);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(exception, "Unexpected exception");

                await Task.Delay(RetryOnFailureDelay.Randomize(), stoppingToken);
            }
        }
    }
}