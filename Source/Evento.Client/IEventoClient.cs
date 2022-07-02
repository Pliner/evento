namespace Evento.Client;

public interface IEventoClient
{
    Task AddSubscriptionAsync(NewSubscriptionDto newSubscription, CancellationToken cancellationToken = default);

    Task SendEventAsync(EventDto @event, CancellationToken cancellationToken = default);

    Task<SubscriptionDto[]?> GetSubscriptionsAsync(CancellationToken cancellationToken = default);
}