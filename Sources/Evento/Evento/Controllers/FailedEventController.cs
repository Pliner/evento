using Evento.Repositories.FailedEvent;
using Evento.Repositories.Subscription;
using Evento.Services;
using Microsoft.AspNetCore.Mvc;

namespace Evento.Controllers;

[ApiController]
[Route("failed-events")]
public class FailedEventController : ControllerBase
{
    private readonly ISubscriptionRepository subscriptionRepository;
    private readonly IFailedEventRepository failedEventRepository;
    private readonly IDirectEventTransport transport;

    public FailedEventController(
        ISubscriptionRepository subscriptionRepository,
        IFailedEventRepository failedEventRepository,
        IDirectEventTransport transport
    )
    {
        this.subscriptionRepository = subscriptionRepository;
        this.failedEventRepository = failedEventRepository;
        this.transport = transport;
    }

    [HttpPost]
    public async Task ResolveAsync(
        [FromQuery] Guid failedEventId,
        [FromQuery] Resolution resolution,
        CancellationToken cancellationToken
    )
    {
        var failedEvent = await failedEventRepository.GetByIdAsync(failedEventId, cancellationToken);
        if (failedEvent is not { Status: FailedEventResolutionStatus.Unresolved }) return;

        switch (resolution)
        {
            case Resolution.Ignore:
                await failedEventRepository.UpdateResolutionStatusByIdAsync(
                    failedEventId,
                    FailedEventResolutionStatus.Ignored,
                    cancellationToken
                );
                return;
            case Resolution.Retry:
            {
                var subscription = await subscriptionRepository.GetByIdAsync(failedEventId, cancellationToken);
                if (subscription == null)
                    throw new Exception("Cannot retry when there is no subscription");

                var actualSubscription = await subscriptionRepository.GetLastVersionByNameAsync(subscription.Name, cancellationToken);
                if (actualSubscription is not { Active: true })
                    throw new Exception("Cannot retry when there is no active subscription");

                await transport.SendAsync(subscription.Endpoint, new Event(failedEvent.Type, failedEvent.Payload), cancellationToken);
            
                await failedEventRepository.UpdateResolutionStatusByIdAsync(
                    failedEventId,
                    FailedEventResolutionStatus.Retried,
                    cancellationToken
                );
                return;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(resolution), resolution, null);
        }
    }
}

public enum Resolution
{
    Ignore,
    Retry,
}