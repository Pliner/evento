using Evento.Internals;
using Evento.Repositories.Subscription;
using Evento.Services;
using Microsoft.AspNetCore.Mvc;

namespace Evento.Controllers;

[ApiController]
[Route("events")]
public class EventController : ControllerBase
{
    private readonly ILogger<EventController> logger;
    private readonly ISubscriptionRepository subscriptionRepository;
    private readonly IEventTransport eventTransport;

    public EventController(
        ILogger<EventController> logger,
        ISubscriptionRepository subscriptionRepository,
        IEventTransport eventTransport
    )
    {
        this.logger = logger;
        this.subscriptionRepository = subscriptionRepository;
        this.eventTransport = eventTransport;
    }

    [HttpPost]
    public async Task SaveAsync(
        [FromQuery] string id,
        [FromQuery] string type,
        [FromQuery] DateTime timestamp,
        CancellationToken cancellationToken
    )
    {
        var subscriptions = await subscriptionRepository.SelectAsync(cancellationToken);
        await using var stream = new ArrayPooledMemoryStream();
        await Request.Body.CopyToAsync(stream, cancellationToken);
        var @event = new Event(id, type, timestamp, stream.Memory);
        foreach (var subscription in subscriptions)
            try
            {
                await eventTransport.TransmitAsync(subscription.Endpoint, @event, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Failed to transmit event {eventId} to endpoint {endpoint}",
                    @event.Id,
                    subscription.Endpoint
                );
            }
    }
}