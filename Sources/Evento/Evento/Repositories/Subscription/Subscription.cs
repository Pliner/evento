namespace Evento.Repositories.Subscription;

// ReSharper disable PropertyCanBeMadeInitOnly.Global
public class Subscription
{
    public string Id { get; set; }
    public string Name { get; set; }
    public int Version { get; set; }
    public DateTime CreatedAt { get; set; }
    public string[] Types { get; set; }
    public string Endpoint { get; set; }
    public bool Active { get; set; }
}
