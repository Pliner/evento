using Evento.Repositories.Subscription;

namespace Evento.Services;

public interface IDirectTransport
{
    Task SendAsync(Subscription subscription, Event @event, CancellationToken cancellationToken = default);
}