namespace Evento.Client;

public interface IEventoClient
{
    Task AddSubscriptionAsync(NewSubscriptionDto newSubscription, CancellationToken cancellationToken = default);

    Task SendEventAsync(EventPropertiesDto properties, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default);

    Task<string[]?> GetSubscriptionsNamesAsync(CancellationToken cancellationToken = default);

    Task<SubscriptionDto?> GetSubscriptionByNameAsync(string subscriptionName, CancellationToken cancellationToken = default);
}