namespace Evento.Services.Transport;

public interface IEventTransport
{
    Task TransmitAsync(string destination, Event @event, CancellationToken cancellationToken);
}