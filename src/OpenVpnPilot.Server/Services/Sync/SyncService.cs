using OpenVpnPilot.Server.Auth;
using OpenVpnPilot.Server.Contracts;
using OpenVpnPilot.Server.Contracts.Responses;
using OpenVpnPilot.Server.Data.Entities;
using OpenVpnPilot.Server.Mapping;
using OpenVpnPilot.Server.Repositories.Interfaces;
using OpenVpnPilot.Server.Services.Vault;

namespace OpenVpnPilot.Server.Services.Sync;

public interface ISyncService
{
    public Task<SyncChangesResponse> ChangesAsync(long since, CancellationToken cancellationToken);
}

public sealed class SyncService(
    IUnitOfWork unitOfWork,
    IProfileRepository profiles,
    ITagRepository tags,
    IVaultRepository vault,
    ISyncRepository sync,
    IVaultCodec codec,
    ICurrentUser currentUser,
    ILogger<SyncService> logger) : ISyncService
{
    public async Task<SyncChangesResponse> ChangesAsync(long since, CancellationToken cancellationToken)
    {
        if (since < 0)
        {
            throw ServiceException.Invalid("since", "The cursor is 0 or a value this server returned.");
        }

        await using ISyncedRead read = await unitOfWork.BeginSyncedReadAsync(cancellationToken);
        long prunedThrough = await sync.PrunedThroughAsync(cancellationToken);

        // A cursor from before the oldest kept deletion, or from after anything this server handed out
        // (a restored backup), cannot be answered with a delta.
        if (since > 0 && (since < prunedThrough || since > read.Cursor))
        {
            SyncLog.CursorExpired(logger, currentUser.Username, since, prunedThrough, read.Cursor);
            throw ServiceException.Gone(ErrorCodes.SyncCursorExpired, "This cursor can no longer be answered. Synchronise again from 0.");
        }

        IReadOnlyList<Profile> changedProfiles = await profiles.ChangedSinceAsync(since, cancellationToken);
        IReadOnlyList<Tag> changedTags = await tags.ChangedSinceAsync(since, cancellationToken);
        IReadOnlyList<VaultEntry> changedVault = await vault.ChangedSinceAsync(since, cancellationToken);
        IReadOnlyList<Tombstone> deletions = since == 0 ? [] : await sync.TombstonesSinceAsync(since, cancellationToken);

        SyncChangesResponse response = new(
            read.Cursor,
            since == 0,
            [.. changedProfiles.Select(p => p.ToResponse())],
            [.. changedTags.Select(t => t.ToResponse())],
            [.. changedVault.Select(codec.Read)],
            [.. deletions.Where(d => d.Kind == TombstoneKind.Profile).Select(d => d.EntityId)],
            [.. deletions.Where(d => d.Kind == TombstoneKind.Tag).Select(d => d.EntityId)],
            [.. deletions.Where(d => d.Kind == TombstoneKind.VaultEntry).Select(d => new VaultKeyResponse(d.EntityId, d.Realm!))]);

        SyncLog.Answered(logger, currentUser.Username, since, read.Cursor, changedProfiles.Count, changedVault.Count, deletions.Count);
        return response;
    }
}
