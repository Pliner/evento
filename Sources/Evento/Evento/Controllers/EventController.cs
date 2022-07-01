using Evento.Internals;
using Evento.Services;
using Microsoft.AspNetCore.Mvc;

namespace Evento.Controllers;

[ApiController]
[Route("events")]
public class EventController : ControllerBase
{
    private readonly IPubSubTransport pubSubTransport;

    public EventController(IPubSubTransport pubSubTransport) => this.pubSubTransport = pubSubTransport;

    [HttpPost]
    public async Task SaveAsync(
        [FromQuery] string type, CancellationToken cancellationToken
    )
    {
        await using var stream = new ArrayPooledMemoryStream();
        await Request.Body.CopyToAsync(stream, cancellationToken);

        var @event = new Event(type, stream.Memory);
        await pubSubTransport.PublishAsync(@event, cancellationToken);
    }
}