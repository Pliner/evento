namespace Evento.Infrastructure;

public sealed class PeriodicBackgroundService<TJob> : BackgroundService where TJob : IPeriodicJob
{
    private readonly ILogger<PeriodicBackgroundService<TJob>> logger;
    private readonly TJob job;

    public PeriodicBackgroundService(ILogger<PeriodicBackgroundService<TJob>> logger, TJob job)
    {
        this.logger = logger;
        this.job = job;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await job.ExecuteAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to execute periodic job {JobName}", job.Name);
            }

            await Task.Delay(job.Interval, stoppingToken);
        }
    }
}