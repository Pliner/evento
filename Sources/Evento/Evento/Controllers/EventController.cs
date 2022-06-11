using Evento.Internals;
using Evento.Services;
using Evento.Services.PubSub;
using Microsoft.AspNetCore.Mvc;

namespace Evento.Controllers;

[ApiController]
[Route("events")]
public class EventController : ControllerBase
{
    private readonly IEventPubSub pubSub;

    public EventController(IEventPubSub pubSub) => this.pubSub = pubSub;

    [HttpPost]
    public async Task SaveAsync(
        [FromQuery] string id,
        [FromQuery] string type,
        [FromQuery] DateTime timestamp,
        CancellationToken cancellationToken
    )
    {
        await using var stream = new ArrayPooledMemoryStream();
        await Request.Body.CopyToAsync(stream, cancellationToken);

        var @event = new Event(id, type, timestamp, stream.Memory);
        await pubSub.PublishAsync(@event, cancellationToken);
    }
}