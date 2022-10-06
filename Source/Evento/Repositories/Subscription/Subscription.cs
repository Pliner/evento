namespace Evento.Repositories.Subscription;

public sealed record Subscription(
    string Name,
    int Version,
    IReadOnlySet<string> Types,
    IReadOnlySet<string> DeletedTypes,
    string Endpoint,
    bool Active
);