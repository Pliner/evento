using Evento.Db;
using Microsoft.EntityFrameworkCore;

namespace Evento.Repositories.Subscription;

public class DbSubscriptionRepository : ISubscriptionRepository
{
    private readonly IDbContextFactory<EventoDbContext> dbContextFactory;

    public DbSubscriptionRepository(IDbContextFactory<EventoDbContext> dbContextFactory) => this.dbContextFactory = dbContextFactory;

    public async Task InsertAsync(Subscription subscription, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = new SubscriptionEntity
        {
            Id = subscription.Id,
            Name = subscription.Name,
            Version = subscription.Version,
            CreatedAt = subscription.CreatedAt,
            Types = subscription.Types,
            Endpoint = subscription.Endpoint,
            Active = subscription.Active,
        };
        await dbContext.AddAsync(entity, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Subscription>> SelectActiveAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entities = await dbContext.Subscriptions.Where(x => x.Active).AsNoTracking().ToListAsync(cancellationToken);
        return entities
            .Select(x => new Subscription
                (
                    Id: x.Id,
                    Name: x.Name,
                    Version: x.Version,
                    CreatedAt: x.CreatedAt,
                    Types: x.Types,
                    Endpoint: x.Endpoint,
                    Active: x.Active
                )
            ).ToList();
    }

    public async Task<Subscription?> GetLastVersionByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entities = await dbContext.Subscriptions
            .Where(x => x.Name == name)
            .OrderByDescending(x => x.Version)
            .Take(1)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return entities
            .Select(x => new Subscription
                (
                    Id: x.Id,
                    Name: x.Name,
                    Version: x.Version,
                    CreatedAt: x.CreatedAt,
                    Types: x.Types,
                    Endpoint: x.Endpoint,
                    Active: x.Active
                )
            ).SingleOrDefault();
    }

    public async Task DeactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await dbContext.Subscriptions.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null) return;

        entity.Active = false;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}