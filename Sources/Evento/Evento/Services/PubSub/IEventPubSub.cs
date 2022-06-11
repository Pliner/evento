using Evento.Repositories.Subscription;

namespace Evento.Services.PubSub;

public interface IConsumer
{
    Task<bool> ShutdownAsync(CancellationToken cancellationToken);
}


public interface IEventPubSub : IDisposable
{
    Task PublishAsync(Event @event, CancellationToken cancellationToken);

    Task<IConsumer> SubscribeAsync(
        Subscription subscription,
        Func<Subscription, Event, CancellationToken, Task> handler,
        CancellationToken cancellationToken
    );
}