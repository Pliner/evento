using Evento.Repositories.Subscription;

namespace Evento.Services;

public interface IPubSubTransport : IDisposable
{
    Task PublishAsync(Event @event, CancellationToken cancellationToken = default);

    IReadOnlySet<Guid> ActiveSubscriptions { get; }

    Task SubscribeAsync(
        Subscription subscription,
        Func<Subscription, Event, CancellationToken, Task> transportFunc,
        CancellationToken cancellationToken = default
    );

    Task<bool> UnsubscribeAsync(Guid subscriptionId, CancellationToken cancellationToken = default);
}