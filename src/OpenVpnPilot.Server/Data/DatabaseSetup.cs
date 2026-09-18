using Microsoft.EntityFrameworkCore;
using OpenVpnPilot.Server.Logging;

namespace OpenVpnPilot.Server.Data;

public static class DatabaseSetup
{
    // No retrying execution strategy: writes run in explicit transactions holding an advisory lock,
    // and a strategy that retries would have to replay those as a unit, which none of them need.
    public static void Configure(DbContextOptionsBuilder builder, string connectionString) =>
        builder
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention();

    public static async Task MigrateAsync(IServiceProvider services, ILogger logger, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        PilotServerDbContext db = scope.ServiceProvider.GetRequiredService<PilotServerDbContext>();

        IReadOnlyList<string> pending = [.. await db.Database.GetPendingMigrationsAsync(cancellationToken)];
        if (pending.Count == 0)
        {
            HostLog.SchemaCurrent(logger);
            return;
        }

        HostLog.Migrating(logger, pending.Count, pending);
        await db.Database.MigrateAsync(cancellationToken);
        HostLog.Migrated(logger);
    }
}
