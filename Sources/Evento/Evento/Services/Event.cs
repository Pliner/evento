namespace Evento.Services;

public readonly record struct Event(
    string Type,
    ReadOnlyMemory<byte> Payload
);