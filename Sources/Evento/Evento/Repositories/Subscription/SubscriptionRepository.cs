namespace Evento.Repositories.Subscription;

public readonly record struct Subscription(
    string Id,
    DateTime CreatedAt,
    string[] Types,
    string Endpoint
);