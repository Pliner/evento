using Evento.Repositories.Subscription;

namespace Evento.Services.SubscriptionRegistry;

public interface ISubscriptionRegistry
{
    IReadOnlySet<string> Registered { get; }

    Task RegisterAsync(Subscription subscription, CancellationToken cancellationToken);

    Task<bool> UnregisterAsync(string subscriptionId, CancellationToken cancellationToken);
}