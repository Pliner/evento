using Evento.Repositories.Subscription;
using Microsoft.AspNetCore.Mvc;

namespace Evento.Controllers;

[ApiController]
[Route("subscriptions")]
public class SubscriptionsController : ControllerBase
{
    private readonly ISubscriptionRepository subscriptionRepository;

    public SubscriptionsController(ISubscriptionRepository subscriptionRepository)
    {
        this.subscriptionRepository = subscriptionRepository;
    }

    [HttpPost]
    public async Task SaveAsync(NewSubscriptionDto subscription, CancellationToken cancellationToken)
    {
        var entity = new Subscription(subscription.Id, DateTime.UtcNow, subscription.Types, subscription.Endpoint);
        await subscriptionRepository.InsertAsync(entity, cancellationToken);
    }

    [HttpGet]
    public async Task<SubscriptionDto[]> SelectAsync(CancellationToken cancellationToken)
    {
        var subscriptions = await subscriptionRepository.SelectAsync(cancellationToken);
        return subscriptions.Select(x => new SubscriptionDto(x.Id, x.CreatedAt, x.Types, x.Endpoint)).ToArray();
    }
}

public readonly record struct NewSubscriptionDto(string Id, string[] Types, string Endpoint);

public readonly record struct SubscriptionDto(string Id, DateTime CreatedAt, string[] Types, string Endpoint);
