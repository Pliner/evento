namespace Evento.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPeriodicJob<TJob>(this IServiceCollection serviceCollection) where TJob : class, IPeriodicJob
    {
        return serviceCollection
            .AddSingleton<TJob>()
            .AddHostedService<PeriodicBackgroundService<TJob>>();
    }
}