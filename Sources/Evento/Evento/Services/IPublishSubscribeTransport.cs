using Evento.Repositories.Subscription;

namespace Evento.Services;

public interface IPublishSubscribeTransport : IDisposable
{
    Task PublishAsync(Event @event, CancellationToken cancellationToken = default);

    IReadOnlySet<Guid> ActiveSubscriptions { get; }

    Task SubscribeAsync(Subscription subscription, CancellationToken cancellationToken = default);

    Task<bool> UnsubscribeAsync(Guid subscriptionId, CancellationToken cancellationToken = default);
}