namespace Evento.Repositories.FailedEvent;

public enum FailedEventResolutionStatus
{
    Unresolved,
    Ignored,
    Retried,
}

public record FailedEvent(
    Guid Id,
    Guid SubscriptionId,
    DateTimeOffset CreatedAt,
    string Type,
    byte[] Payload,
    FailedEventResolutionStatus Status = FailedEventResolutionStatus.Unresolved
);
