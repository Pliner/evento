using Evento.Internals;
using Evento.Services;
using Microsoft.AspNetCore.Mvc;

namespace Evento.Controllers;

[ApiController]
[Route("api/events")]
public class EventController : ControllerBase
{
    private readonly IPublishSubscribe publishSubscribe;

    public EventController(IPublishSubscribe publishSubscribe) => this.publishSubscribe = publishSubscribe;

    [HttpPost]
    public async Task PublishAsync([FromQuery] string type, CancellationToken cancellationToken)
    {
        await using var stream = new ArrayPooledMemoryStream();
        await Request.Body.CopyToAsync(stream, cancellationToken);

        var properties = new EventProperties(type, Request.ContentType ?? "application/json");
        var readOnlyMemory = stream.Memory;
        await publishSubscribe.PublishAsync(properties, readOnlyMemory, cancellationToken);
    }
}