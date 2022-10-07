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

    Task MaintainSubscriptionAsync(Subscription subscription, EventHandlerDelegate transportFunc, CancellationToken cancellationToken = default);

    Task InterruptSubscriptionsAsync(CancellationToken cancellationToken = default);
}