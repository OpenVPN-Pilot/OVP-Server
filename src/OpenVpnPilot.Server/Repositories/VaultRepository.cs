using Microsoft.EntityFrameworkCore;
using OpenVpnPilot.Server.Data;
using OpenVpnPilot.Server.Data.Entities;
using OpenVpnPilot.Server.Repositories.Interfaces;

namespace OpenVpnPilot.Server.Repositories;

public sealed class VaultRepository(PilotServerDbContext db) : IVaultRepository
{
    public async Task<IReadOnlyList<VaultEntry>> ListAsync(Guid? profileId, CancellationToken cancellationToken)
    {
        IQueryable<VaultEntry> query = db.VaultEntries.AsNoTracking();
        if (profileId is not null)
        {
            query = query.Where(v => v.ProfileId == profileId);
        }

        return await query.OrderBy(v => v.ProfileId).ThenBy(v => v.Realm).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<VaultEntry>> ChangedSinceAsync(long changeSeq, CancellationToken cancellationToken) =>
        await db.VaultEntries.AsNoTracking().Where(v => v.ChangeSeq > changeSeq).OrderBy(v => v.ChangeSeq).ToListAsync(cancellationToken);

    public Task<VaultEntry?> FindAsync(Guid profileId, string realm, CancellationToken cancellationToken) =>
        db.VaultEntries.FirstOrDefaultAsync(v => v.ProfileId == profileId && v.Realm == realm, cancellationToken);

    public void Add(VaultEntry entry) => db.VaultEntries.Add(entry);

    public void Remove(VaultEntry entry) => db.VaultEntries.Remove(entry);
}
