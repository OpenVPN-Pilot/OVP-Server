using OpenVpnPilot.Server.Data.Entities;

namespace OpenVpnPilot.Server.Repositories.Interfaces;

public interface ISyncRepository
{
    public void AddTombstone(Tombstone tombstone);

    public Task<IReadOnlyList<Tombstone>> TombstonesSinceAsync(long changeSeq, CancellationToken cancellationToken);

    public Task<long> PrunedThroughAsync(CancellationToken cancellationToken);

    // Removes tombstones older than the cut-off and records the newest change number that went with them.
    public Task<int> PruneTombstonesAsync(DateTimeOffset before, CancellationToken cancellationToken);
}

public interface IUserPreferenceRepository
{
    public Task<IReadOnlyList<UserFavourite>> FavouritesAsync(Guid userId, CancellationToken cancellationToken);

    public Task ReplaceFavouritesAsync(Guid userId, IReadOnlyList<UserFavourite> favourites, CancellationToken cancellationToken);

    public Task<IReadOnlyList<UserHotkey>> HotkeysAsync(Guid userId, CancellationToken cancellationToken);

    public Task ReplaceHotkeysAsync(Guid userId, IReadOnlyList<UserHotkey> hotkeys, CancellationToken cancellationToken);

    public Task<UserSettingsDocument?> SettingsAsync(Guid userId, CancellationToken cancellationToken);

    public void AddSettings(UserSettingsDocument settings);

    // Makes the next save fail unless the stored version is still the one the client last read.
    public void ExpectSettingsVersion(UserSettingsDocument settings, uint version);
}
