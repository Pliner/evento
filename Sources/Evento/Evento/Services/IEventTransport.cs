namespace Evento.Services;

public interface IEventTransport
{
    Task TransmitAsync(string destination, Event @event, CancellationToken cancellationToken);
}