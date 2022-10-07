namespace Evento.Services;

public interface ISubscriptionManager
{
    Task RunAsync(CancellationToken cancellationToken = default);
}