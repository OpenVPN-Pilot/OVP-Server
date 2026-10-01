using OpenVpnPilot.Server.Auth;
using OpenVpnPilot.Server.Contracts;
using OpenVpnPilot.Server.Contracts.Requests;
using OpenVpnPilot.Server.Contracts.Responses;
using OpenVpnPilot.Server.Data.Entities;
using OpenVpnPilot.Server.Repositories.Interfaces;

namespace OpenVpnPilot.Server.Services.Vault;

public interface IVaultService
{
    public Task<IReadOnlyList<VaultEntryResponse>> ListAsync(Guid? profileId, CancellationToken cancellationToken);

    // Anyone may add a sign in that is not there yet; that is what spares every newcomer typing them all.
    public Task<VaultEntryResponse> AddAsync(Guid profileId, string realm, VaultEntryRequest request, CancellationToken cancellationToken);

    // Replacing or removing one that exists is an administrator's decision.
    public Task<VaultEntryResponse> ReplaceAsync(Guid profileId, string realm, VaultEntryRequest request, CancellationToken cancellationToken);

    public Task DeleteAsync(Guid profileId, string realm, CancellationToken cancellationToken);
}

public sealed class VaultService(
    IVaultRepository vault,
    IProfileRepository profiles,
    ISyncRepository sync,
    IUnitOfWork unitOfWork,
    IVaultCodec codec,
    ICurrentUser currentUser,
    TimeProvider time,
    ILogger<VaultService> logger) : IVaultService
{
    public const int MaximumRealmLength = 200;

    public async Task<IReadOnlyList<VaultEntryResponse>> ListAsync(Guid? profileId, CancellationToken cancellationToken)
    {
        if (profileId is not null)
        {
            await RequireProfileAsync(profileId.Value, cancellationToken);
        }

        IReadOnlyList<VaultEntry> entries = await vault.ListAsync(profileId, cancellationToken);
        VaultLog.Read(logger, currentUser.Username, entries.Count, profileId);
        return [.. entries.Select(codec.Read)];
    }

    public async Task<VaultEntryResponse> AddAsync(
        Guid profileId, string realm, VaultEntryRequest request, CancellationToken cancellationToken)
    {
        realm = Realm(realm);
        await using ISyncedWrite write = await unitOfWork.BeginSyncedWriteAsync(cancellationToken);
        await RequireProfileAsync(profileId, cancellationToken);
        if (await vault.FindAsync(profileId, realm, cancellationToken) is not null)
        {
            throw ServiceException.Conflict(ErrorCodes.VaultEntryExists,
                $"The vault already holds a sign in for realm '{realm}' of this profile. Only an administrator can replace it.");
        }

        DateTimeOffset now = time.GetUtcNow();
        VaultEntry entry = new()
        {
            ProfileId = profileId,
            Realm = realm,
            ChangeSeq = write.ChangeSeq,
            CreatedAt = now,
            CreatedBy = currentUser.Username,
            UpdatedAt = now,
            UpdatedBy = currentUser.Username,
        };
        codec.Write(entry, Blank(request.Username), request.Password);
        vault.Add(entry);
        await sync.RemoveVaultTombstonesAsync(profileId, realm, cancellationToken);
        await write.CommitAsync(cancellationToken);
        VaultLog.Added(logger, currentUser.Username, profileId, realm, write.ChangeSeq);
        return codec.Read(entry);
    }

    public async Task<VaultEntryResponse> ReplaceAsync(
        Guid profileId, string realm, VaultEntryRequest request, CancellationToken cancellationToken)
    {
        realm = Realm(realm);
        await using ISyncedWrite write = await unitOfWork.BeginSyncedWriteAsync(cancellationToken);
        await RequireProfileAsync(profileId, cancellationToken);
        VaultEntry? entry = await vault.FindAsync(profileId, realm, cancellationToken);
        DateTimeOffset now = time.GetUtcNow();
        if (entry is null)
        {
            entry = new VaultEntry { ProfileId = profileId, Realm = realm, CreatedAt = now, CreatedBy = currentUser.Username };
            vault.Add(entry);
            await sync.RemoveVaultTombstonesAsync(profileId, realm, cancellationToken);
        }

        entry.ChangeSeq = write.ChangeSeq;
        entry.UpdatedAt = now;
        entry.UpdatedBy = currentUser.Username;
        codec.Write(entry, Blank(request.Username), request.Password);
        await write.CommitAsync(cancellationToken);
        VaultLog.Replaced(logger, currentUser.Username, profileId, realm, write.ChangeSeq);
        return codec.Read(entry);
    }

    public async Task DeleteAsync(Guid profileId, string realm, CancellationToken cancellationToken)
    {
        realm = Realm(realm);
        await using ISyncedWrite write = await unitOfWork.BeginSyncedWriteAsync(cancellationToken);
        VaultEntry entry = await vault.FindAsync(profileId, realm, cancellationToken)
            ?? throw ServiceException.NotFound(ErrorCodes.VaultEntryNotFound, $"There is no vault entry for realm '{realm}' of profile {profileId}.");
        vault.Remove(entry);
        sync.AddTombstone(new Tombstone
        {
            Kind = TombstoneKind.VaultEntry,
            EntityId = profileId,
            Realm = realm,
            ChangeSeq = write.ChangeSeq,
            DeletedAt = time.GetUtcNow(),
        });
        await write.CommitAsync(cancellationToken);
        VaultLog.Deleted(logger, currentUser.Username, profileId, realm, write.ChangeSeq);
    }

    private async Task RequireProfileAsync(Guid profileId, CancellationToken cancellationToken)
    {
        if ((await profiles.ExistingIdsAsync([profileId], cancellationToken)).Count == 0)
        {
            throw ServiceException.NotFound(ErrorCodes.ProfileNotFound, $"There is no profile {profileId}.");
        }
    }

    // The realm is the client's keystore key as OpenVPN named it, so it is taken exactly as sent and
    // refused rather than tidied: a trimmed realm would never match the one the client looks up.
    private static string Realm(string realm)
    {
        // Routing decodes every escape in a path segment except a slash, which it leaves as %2F.
        string decoded = realm.Replace("%2F", "/", StringComparison.OrdinalIgnoreCase);
        bool valid = decoded.Length is > 0 and <= MaximumRealmLength
            && !decoded.Any(char.IsControl)
            && decoded.Trim().Length == decoded.Length;
        return valid
            ? decoded
            : throw ServiceException.Invalid(
                "realm", $"A realm is 1 to {MaximumRealmLength} printable characters, without spaces at either end.");
    }

    private static string? Blank(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
