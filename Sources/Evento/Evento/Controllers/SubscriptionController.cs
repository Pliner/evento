using Evento.Repositories.Subscription;
using Microsoft.AspNetCore.Mvc;

namespace Evento.Controllers;

[ApiController]
[Route("subscriptions")]
public class SubscriptionsController : ControllerBase
{
    private readonly ISubscriptionRepository subscriptionRepository;

    public SubscriptionsController(ISubscriptionRepository subscriptionRepository) => this.subscriptionRepository = subscriptionRepository;

    [HttpPost]
    public async Task SaveAsync([FromBody] NewSubscriptionDto subscription, CancellationToken cancellationToken)
    {
        var activeSubscription = await subscriptionRepository.GetActiveByNameAsync(subscription.Name, cancellationToken);

        if (
            activeSubscription.HasValue && activeSubscription.Value.Endpoint == subscription.Endpoint
            && activeSubscription.Value.Types.SequenceEqual(subscription.Types)
        )
            return;

        var newSubscription = new Subscription(
            Guid.NewGuid().ToString(),
            subscription.Name,
            (activeSubscription?.Version ?? 0) + 1,
            DateTime.UtcNow,
            subscription.Types,
            subscription.Endpoint
        );
        await subscriptionRepository.InsertAsync(newSubscription, cancellationToken);
    }

    [HttpGet]
    public async Task<SubscriptionDto[]> SelectAsync(CancellationToken cancellationToken)
    {
        var subscriptions = await subscriptionRepository.SelectActiveAsync(cancellationToken);

        return subscriptions
            .GroupBy(x => x.Name, x => x, (_, items) => items.MaxBy(x => x.Version))
            .Select(x => new SubscriptionDto(x.Name, x.CreatedAt, x.Types, x.Endpoint))
            .ToArray();
    }
}

public readonly record struct NewSubscriptionDto(string Name, string[] Types, string Endpoint);

public readonly record struct SubscriptionDto(
    string Name,
    DateTime CreatedAt,
    string[] Types,
    string Endpoint
);