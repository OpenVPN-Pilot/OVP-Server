namespace OpenVpnPilot.Server.Contracts.Responses;

/// <summary>Everything that changed after a cursor. Apply it, then keep <c>Cursor</c> for the next call.</summary>
/// <param name="Cursor">Pass this as <c>since</c> next time. Every change up to it is contained in this or an earlier answer.</param>
/// <param name="Full">True when <c>since</c> was 0: this is the complete state, and anything the client holds from this server that is not in it is gone.</param>
/// <param name="Profiles">Profiles created or changed. Fetch the configuration again when <c>ContentHash</c> differs from the one held.</param>
/// <param name="Tags">Tags created or changed.</param>
/// <param name="VaultEntries">Vault entries created or changed, with their secrets.</param>
/// <param name="DeletedProfiles">Profiles removed. Their vault entries are removed with them.</param>
/// <param name="DeletedTags">Tags removed.</param>
/// <param name="DeletedVaultEntries">Vault entries removed while their profile stayed.</param>
public sealed record SyncChangesResponse(
    long Cursor,
    bool Full,
    IReadOnlyList<ProfileResponse> Profiles,
    IReadOnlyList<TagResponse> Tags,
    IReadOnlyList<VaultEntryResponse> VaultEntries,
    IReadOnlyList<Guid> DeletedProfiles,
    IReadOnlyList<Guid> DeletedTags,
    IReadOnlyList<VaultKeyResponse> DeletedVaultEntries);

/// <summary>Identifies a vault entry.</summary>
/// <param name="ProfileId">The profile.</param>
/// <param name="Realm">The realm.</param>
public sealed record VaultKeyResponse(Guid ProfileId, string Realm);
