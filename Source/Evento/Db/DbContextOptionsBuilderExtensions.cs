using Microsoft.EntityFrameworkCore;

namespace Evento.Db;

public static class DbContextOptionsBuilderExtensions
{
    public static TBuilder SetupPostgresql<TBuilder>(this TBuilder builder, IConfiguration configuration) where TBuilder : DbContextOptionsBuilder
    {
        var host = configuration["POSTGRES_HOST"] ?? "pg";
        var port = configuration["POSTGRES_PORT"] ?? "5432";
        var user = configuration["POSTGRES_USER"] ?? "postgres";
        var database = configuration["POSTGRES_DATABASE"] ?? "postgres";
        var password = configuration["POSTGRES_PASSWORD"] ?? "some_secret";
        var connectionString =
            $"User Id={user};Host={host};Port={port};Database={database};Password={password};Pooling=true;";
        builder.UseNpgsql(connectionString);
        builder.UseSnakeCaseNamingConvention();
        return builder;
    }
}