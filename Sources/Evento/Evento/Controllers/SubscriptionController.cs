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
        var existingSubscription = await subscriptionRepository.GetLastVersionByNameAsync(subscription.Name, cancellationToken);

        if (
            existingSubscription != null
            && existingSubscription.Endpoint == subscription.Endpoint
            && existingSubscription.Types.SequenceEqual(subscription.Types)
            && existingSubscription.Active
        )
            return;

        var newSubscription = new Subscription
        (
            Id: Guid.NewGuid(),
            Name: subscription.Name,
            Version: (existingSubscription?.Version ?? 0) + 1,
            CreatedAt: DateTimeOffset.UtcNow,
            Types: subscription.Types,
            Endpoint: subscription.Endpoint,
            Active: true
        );
        await subscriptionRepository.InsertAsync(newSubscription, cancellationToken);
    }

    [HttpGet]
    public async Task<SubscriptionDto[]> SelectAsync(CancellationToken cancellationToken)
    {
        var subscriptions = await subscriptionRepository.SelectActiveAsync(cancellationToken);

        return subscriptions
            .GroupBy(x => x.Name, x => x, (_, items) => items.MaxBy(x => x.Version)!)
            .Select(x => new SubscriptionDto(x.Name, x.CreatedAt, x.Types, x.Endpoint))
            .ToArray();
    }
}

public record NewSubscriptionDto(string Name, string[] Types, string Endpoint);

public record SubscriptionDto(
    string Name,
    DateTimeOffset CreatedAt,
    string[] Types,
    string Endpoint
);