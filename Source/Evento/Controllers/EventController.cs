using Evento.Internals;
using Evento.Services;
using Microsoft.AspNetCore.Mvc;

namespace Evento.Controllers;

[ApiController]
[Route("api/events")]
public class EventController : ControllerBase
{
    private readonly IPublishSubscribeTransport publishSubscribeTransport;

    public EventController(IPublishSubscribeTransport publishSubscribeTransport) => this.publishSubscribeTransport = publishSubscribeTransport;

    [HttpPost]
    public async Task SaveAsync(
        [FromQuery] string type, CancellationToken cancellationToken
    )
    {
        await using var stream = new ArrayPooledMemoryStream();
        await Request.Body.CopyToAsync(stream, cancellationToken);

        var @event = new Event(type, stream.Memory);
        await publishSubscribeTransport.PublishAsync(@event, cancellationToken);
    }
}