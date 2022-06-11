namespace Evento.Infrastructure;

public interface IPeriodicJob
{
    string Name { get; }
    TimeSpan Interval { get; }
    Task ExecuteAsync(CancellationToken cancellationToken = default);
}