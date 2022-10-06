namespace Evento.Client;

public readonly record struct NewSubscriptionDto(string Name, string[] Types, string Endpoint);

public readonly record struct SubscriptionDto(string Name, string[] Types, string Endpoint, bool Active);

public readonly record struct EventPropertiesDto(string Type, string ContentType = "application/json");