using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OpenVpnPilot.Server.Data;

// Lets 'dotnet ef migrations add' build the model without the environment a running server needs.
// The connection string is never opened when a migration is only being written.
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PilotServerDbContext>
{
    public PilotServerDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<PilotServerDbContext> builder = new();
        DatabaseSetup.Configure(builder, "Host=localhost;Database=ovp;Username=ovp;Password=design-time");
        return new PilotServerDbContext(builder.Options);
    }
}
