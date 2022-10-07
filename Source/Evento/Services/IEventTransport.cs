using Evento.Repositories.Subscription;

namespace Evento.Services;

public interface IEventTransport
{
    Task SendAsync(
        Subscription subscription,
        EventProperties properties,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default
    );
}