namespace Evento.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPeriodicJob<TPeriodicJob>(
        this IServiceCollection serviceCollection
    ) where TPeriodicJob : class, IPeriodicJob
    {
        serviceCollection.AddSingleton<TPeriodicJob>();
        return serviceCollection.AddHostedService(
            x => new PeriodicBackgroundService<TPeriodicJob>(
                x.GetRequiredService<ILogger<PeriodicBackgroundService<TPeriodicJob>>>(),
                x.GetRequiredService<TPeriodicJob>()
            )
        );
    }
}