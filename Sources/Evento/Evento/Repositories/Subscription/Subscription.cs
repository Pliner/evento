namespace Evento.Repositories.Subscription;

public record Subscription(
    string Id,
    string Name,
    int Version,
    DateTimeOffset CreatedAt,
    string[] Types,
    string Endpoint,
    bool Active = true
);