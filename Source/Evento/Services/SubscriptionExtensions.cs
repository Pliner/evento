using Evento.Repositories.Subscription;

namespace Evento.Services;

public static class SubscriptionExtensions
{
    public static string GetQueueName(this Subscription subscription) => $"evento:{subscription.Name}";
    public static string GetFailedQueueName(this Subscription subscription) => $"evento:{subscription.Name}:failed";
}