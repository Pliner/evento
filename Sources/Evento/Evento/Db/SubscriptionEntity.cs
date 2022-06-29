namespace Evento.Db;

// ReSharper disable PropertyCanBeMadeInitOnly.Global
public class SubscriptionEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public int Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string[] Types { get; set; } = null!;
    public string Endpoint { get; set; } = null!;
    public bool Active { get; set; }
}