using Microsoft.EntityFrameworkCore;
using OpenVpnPilot.Server.Data;
using OpenVpnPilot.Server.Data.Entities;
using OpenVpnPilot.Server.Repositories.Interfaces;

namespace OpenVpnPilot.Server.Repositories;

public sealed class SyncRepository(PilotServerDbContext db) : ISyncRepository
{
    public void AddTombstone(Tombstone tombstone) => db.Tombstones.Add(tombstone);

    public async Task<IReadOnlyList<Tombstone>> TombstonesSinceAsync(long changeSeq, CancellationToken cancellationToken) =>
        await db.Tombstones.AsNoTracking().Where(t => t.ChangeSeq > changeSeq).OrderBy(t => t.ChangeSeq).ToListAsync(cancellationToken);

    public Task<long> PrunedThroughAsync(CancellationToken cancellationToken) =>
        db.SyncStates.Select(s => s.PrunedThrough).FirstAsync(cancellationToken);

    public async Task<int> PruneTombstonesAsync(DateTimeOffset before, CancellationToken cancellationToken)
    {
        long? newest = await db.Tombstones.Where(t => t.DeletedAt < before).MaxAsync(t => (long?)t.ChangeSeq, cancellationToken);
        if (newest is null)
        {
            return 0;
        }

        await db.SyncStates
            .Where(s => s.PrunedThrough < newest)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.PrunedThrough, newest.Value), cancellationToken);
        return await db.Tombstones.Where(t => t.ChangeSeq <= newest).ExecuteDeleteAsync(cancellationToken);
    }
}
