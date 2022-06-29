namespace Evento.Db;

public class FailedEventEntity
{
    public Guid Id { get; set; }
    public Guid SubscriptionId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string Type { get; set; } = null!;
    public byte[] Payload { get; set; } = null!;
}
