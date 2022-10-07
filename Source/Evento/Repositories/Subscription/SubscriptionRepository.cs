using Evento.Db;
using Microsoft.EntityFrameworkCore;

namespace Evento.Repositories.Subscription;

public class SubscriptionRepository : ISubscriptionRepository
{
    private readonly IDbContextFactory<EventoDbContext> dbContextFactory;

    public SubscriptionRepository(IDbContextFactory<EventoDbContext> dbContextFactory) => this.dbContextFactory = dbContextFactory;

    public async Task AddAsync(NewSubscriptionData subscriptionData, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            Name = subscriptionData.Name,
            Version = subscriptionData.Version,
            CreatedAt = DateTimeOffset.UtcNow,
            Types = subscriptionData.Types.ToArray(),
            Endpoint = subscriptionData.Endpoint,
            Active = true,
        };
        await dbContext.AddAsync(entity, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetNotActiveAsync(Subscription subscription, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            Name = subscription.Name,
            Version = subscription.Version + 1,
            CreatedAt = DateTimeOffset.UtcNow,
            Types = subscription.Types.ToArray(),
            Endpoint = subscription.Endpoint,
            Active = false
        };
        await dbContext.AddAsync(entity, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> SelectNamesAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.Subscriptions
            .Select(x => x.Name)
            .Distinct()
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<Subscription?> TryGetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entities = await dbContext.Subscriptions
            .Where(x => x.Name == name)
            .OrderBy(x => x.Version)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return entities.Count == 0 ? null : BuildSubscription(name, entities);
    }

    private static Subscription BuildSubscription(string name, IReadOnlyList<SubscriptionEntity> entities)
    {
        var types = new HashSet<string>();
        var deletedTypes = new HashSet<string>();
        foreach (var entity in entities)
        {
            deletedTypes.UnionWith(types);

            deletedTypes.ExceptWith(entity.Types);
            types = entity.Types.ToHashSet();
        }

        return new Subscription(name, entities[^1].Version, types, deletedTypes, entities[^1].Endpoint, entities[^1].Active);
    }
}