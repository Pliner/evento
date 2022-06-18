using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Evento.Db;

// ReSharper disable once UnusedType.Global
public class EventoDbContextFactory : IDesignTimeDbContextFactory<EventoDbContext>
{
    public EventoDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder().AddEnvironmentVariables();
        var optionsBuilder = new DbContextOptionsBuilder<EventoDbContext>()
            .SetupPostgresql(configuration.Build());
        return new EventoDbContext(optionsBuilder.Options);
    }
}
