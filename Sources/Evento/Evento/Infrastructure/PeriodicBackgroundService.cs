namespace Evento.Infrastructure;

public class PeriodicBackgroundService<TPeriodicService> : BackgroundService where TPeriodicService : IPeriodicJob
{
    private readonly ILogger<PeriodicBackgroundService<TPeriodicService>> logger;
    private readonly TPeriodicService periodicJob;

    public PeriodicBackgroundService(
        ILogger<PeriodicBackgroundService<TPeriodicService>> logger,
        TPeriodicService periodicJob
    )
    {
        this.logger = logger;
        this.periodicJob = periodicJob;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await periodicJob.ExecuteAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to execute periodic job {PeriodicJobName}", periodicJob.Name);
            }

            await Task.Delay(periodicJob.Interval, stoppingToken);
        }
    }
}