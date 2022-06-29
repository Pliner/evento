namespace Evento.Repositories.Subscription;

public record Subscription(
    Guid Id,
    string Name,
    int Version,
    DateTimeOffset CreatedAt,
    string[] Types,
    string Endpoint,
    bool Active = true
);