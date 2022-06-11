namespace Evento.Services;

public readonly record struct Event(string Id, string Type, DateTime Timestamp, ReadOnlyMemory<byte> Payload);