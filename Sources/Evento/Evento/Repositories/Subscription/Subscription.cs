namespace Evento.Repositories.Subscription;

public readonly record struct Subscription(
    string Id,
    string Name,
    int Version,
    DateTime CreatedAt,
    string[] Types,
    string Endpoint,
    bool Active = true
);
