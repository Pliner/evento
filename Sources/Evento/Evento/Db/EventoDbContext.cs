using Microsoft.EntityFrameworkCore;

namespace Evento.Db;

public class EventoDbContext : DbContext
{

    public EventoDbContext(DbContextOptions<EventoDbContext> options) : base(options)
    {
    }

    public DbSet<SubscriptionEntity> Subscriptions { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SubscriptionEntity>().HasIndex(x => new { x.Name, x.Version }).IsUnique();
    }
}