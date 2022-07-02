namespace Evento.Client;

public readonly record struct NewSubscriptionDto(string Name, string[] Types, string Endpoint);

public readonly record struct SubscriptionDto(
    string Name,
    DateTimeOffset CreatedAt,
    string[] Types,
    string Endpoint
);

public readonly record struct EventDto(string Type, ReadOnlyMemory<byte> Payload);
