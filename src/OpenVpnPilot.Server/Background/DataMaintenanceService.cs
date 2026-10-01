using OpenVpnPilot.Server.Logging;
using OpenVpnPilot.Server.Repositories.Interfaces;

namespace OpenVpnPilot.Server.Background;

// Removes refresh tokens nobody can use any more and deletion records old enough that every client
// still synchronising has seen them. A client away for longer starts over from a full synchronisation.
public sealed class DataMaintenanceService(
    IServiceScopeFactory scopes,
    TimeProvider time,
    ILogger<DataMaintenanceService> logger) : BackgroundService
{
    public static readonly TimeSpan TombstoneLifetime = TimeSpan.FromDays(90);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromHours(1), time);
        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // A failed pass is reported and retried; the server keeps serving requests meanwhile.
                HostLog.MaintenanceFailed(logger, exception);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        IRefreshTokenRepository tokens = scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>();
        ISyncRepository sync = scope.ServiceProvider.GetRequiredService<ISyncRepository>();
        IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        DateTimeOffset now = time.GetUtcNow();

        int expired = await tokens.DeleteExpiredAsync(now.AddDays(-1), cancellationToken);

        // A synchronisation that has already compared its cursor with the pruned mark must not then find
        // the deletions it relies on gone.
        int pruned;
        await using (IWriteTransaction transaction = await unitOfWork.BeginExclusiveAsync(cancellationToken))
        {
            pruned = await sync.PruneTombstonesAsync(now - TombstoneLifetime, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        if (expired > 0 || pruned > 0)
        {
            HostLog.MaintenanceDone(logger, expired, pruned);
        }
    }
}
