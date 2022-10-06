using Evento.Repositories.Subscription;

namespace Evento.Services;

public enum EventHandlerResult
{
    Processed,
    Failed
}

public delegate Task<EventHandlerResult> EventHandlerDelegate(
    Subscription subscription,
    EventProperties properties,
    ReadOnlyMemory<byte> payload,
    CancellationToken cancellationToken = default
);

public interface IPublishSubscribe : IDisposable
{
    Task PublishAsync(EventProperties properties, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default);

    IReadOnlySet<string> ActiveSubscriptions { get; }

    Task StartSubscriptionAsync(Subscription subscription, EventHandlerDelegate transportFunc, CancellationToken cancellationToken = default);

    Task RefreshSubscriptionAsync(Subscription subscription, CancellationToken cancellationToken = default);

    Task DeactivateSubscriptionAsync(Subscription subscription, CancellationToken cancellationToken = default);
}