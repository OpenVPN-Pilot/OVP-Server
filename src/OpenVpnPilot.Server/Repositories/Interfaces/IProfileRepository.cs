using OpenVpnPilot.Server.Data.Entities;

namespace OpenVpnPilot.Server.Repositories.Interfaces;

public interface IProfileRepository
{
    public Task<IReadOnlyList<Profile>> ListAsync(string? tag, string? search, CancellationToken cancellationToken);

    public Task<IReadOnlyList<Profile>> ChangedSinceAsync(long changeSeq, CancellationToken cancellationToken);

    public Task<Profile?> FindAsync(Guid id, bool withContent, CancellationToken cancellationToken);

    // The name of another profile with this exact configuration, if there is one.
    public Task<string?> FindNameByHashAsync(string contentHash, Guid? except, CancellationToken cancellationToken);

    public Task<IReadOnlySet<Guid>> ExistingIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken);

    public void Add(Profile profile);

    public void Remove(Profile profile);

    // Makes the next save fail unless the stored version is still the one the client last read.
    public void ExpectVersion(Profile profile, uint version);
}

public interface ITagRepository
{
    public Task<IReadOnlyList<Tag>> ListAsync(CancellationToken cancellationToken);

    public Task<IReadOnlyList<Tag>> ChangedSinceAsync(long changeSeq, CancellationToken cancellationToken);

    public Task<Tag?> FindAsync(Guid id, bool withProfiles, CancellationToken cancellationToken);

    public Task<IReadOnlyList<Tag>> FindByNamesAsync(IReadOnlyCollection<string> names, CancellationToken cancellationToken);

    public Task<bool> NameTakenAsync(string name, Guid? except, CancellationToken cancellationToken);

    public void Add(Tag tag);

    public void Remove(Tag tag);
}

public interface IVaultRepository
{
    public Task<IReadOnlyList<VaultEntry>> ListAsync(Guid? profileId, CancellationToken cancellationToken);

    public Task<IReadOnlyList<VaultEntry>> ChangedSinceAsync(long changeSeq, CancellationToken cancellationToken);

    public Task<VaultEntry?> FindAsync(Guid profileId, string realm, CancellationToken cancellationToken);

    public void Add(VaultEntry entry);

    public void Remove(VaultEntry entry);
}
