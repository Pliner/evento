using Evento.Repositories.Subscription;

namespace Evento.Services.SubscriptionRegistry;

public interface ISubscriptionRegistry
{
    IReadOnlySet<Guid> Registered { get; }

    Task RegisterAsync(Subscription subscription, CancellationToken cancellationToken = default);

    Task<bool> UnregisterAsync(Guid subscriptionId, CancellationToken cancellationToken = default);
}