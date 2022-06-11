using Evento.Repositories.Subscription;

namespace Evento.Services.PubSub;

public interface IConsumer
{
    Task<bool> ShutdownAsync(CancellationToken cancellationToken = default);
}


public interface IEventPubSub : IDisposable
{
    Task PublishAsync(Event @event, CancellationToken cancellationToken = default);

    Task<IConsumer> SubscribeAsync(
        Subscription subscription,
        Func<Subscription, Event, CancellationToken, Task> handler,
        CancellationToken cancellationToken
    );
}