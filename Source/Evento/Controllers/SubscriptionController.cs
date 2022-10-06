using Evento.Client;
using Evento.Repositories.Subscription;
using Microsoft.AspNetCore.Mvc;

namespace Evento.Controllers;

[ApiController]
[Route("api/subscriptions")]
public class SubscriptionsController : ControllerBase
{
    private readonly ISubscriptionRepository subscriptionRepository;

    public SubscriptionsController(ISubscriptionRepository subscriptionRepository) => this.subscriptionRepository = subscriptionRepository;

    [HttpPost]
    public async Task AddAsync([FromBody] NewSubscriptionDto subscription, CancellationToken cancellationToken)
    {
        var existingSubscription = await subscriptionRepository.TryGetByNameAsync(subscription.Name, cancellationToken);

        if (
            existingSubscription != null
            && existingSubscription.Endpoint == subscription.Endpoint
            && existingSubscription.Types.SequenceEqual(subscription.Types)
            && existingSubscription.Active
        )
            return;

        var newSubscription = new NewSubscriptionData
        (
            Name: subscription.Name,
            Version: (existingSubscription?.Version ?? 0) + 1,
            Types: subscription.Types.ToHashSet(),
            Endpoint: subscription.Endpoint
        );
        await subscriptionRepository.AddAsync(newSubscription, cancellationToken);
    }

    [HttpGet]
    public async Task<IReadOnlyList<string>> SelectNamesAsync(CancellationToken cancellationToken)
    {
        return await subscriptionRepository.SelectNamesAsync(cancellationToken);
    }

    [HttpGet("{subscriptionName}")]
    public async Task<SubscriptionDto?> TryGetByNameAsync([FromRoute] string subscriptionName, CancellationToken cancellationToken)
    {
        var subscription = await subscriptionRepository.TryGetByNameAsync(subscriptionName, cancellationToken);
        if (subscription == null) return null;

        return new SubscriptionDto(subscription.Name, subscription.Types.ToArray(), subscription.Endpoint, subscription.Active);
    }

    [HttpDelete("{subscriptionName}")]
    public async Task SetNotActiveAsync([FromRoute] string subscriptionName, CancellationToken cancellationToken)
    {
        var subscription = await subscriptionRepository.TryGetByNameAsync(subscriptionName, cancellationToken);
        if (subscription is not { Active: true }) return;

        await subscriptionRepository.SetNotActiveAsync(subscription, cancellationToken);
    }
}