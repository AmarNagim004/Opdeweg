using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Opdeweg.Infrastructure.Persistence;

/// <summary>Used by <c>dotnet ef</c> only. Reads DATABASE_CONNECTION_STRING, falling back to the docker-compose default.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=opdeweg;Username=opdeweg;Password=opdeweg";

        var options = new DbContextOptionsBuilder<AppDbContext>();
        PersistenceSetup.Configure(options, connectionString);
        return new AppDbContext(options.Options);
    }
}

internal static class PersistenceSetup
{
    public static void Configure(DbContextOptionsBuilder options, string connectionString) =>
        options
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.UseNetTopologySuite();
                npgsql.EnableRetryOnFailure(3);
                npgsql.MigrationsHistoryTable("__ef_migrations_history");
            })
            .UseSnakeCaseNamingConvention();
}
