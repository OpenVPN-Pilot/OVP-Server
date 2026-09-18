using Microsoft.EntityFrameworkCore;
using OpenVpnPilot.Server.Data;
using OpenVpnPilot.Server.Data.Entities;
using OpenVpnPilot.Server.Repositories.Interfaces;

namespace OpenVpnPilot.Server.Repositories;

public sealed class UserPreferenceRepository(PilotServerDbContext db) : IUserPreferenceRepository
{
    public async Task<IReadOnlyList<UserFavourite>> FavouritesAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.UserFavourites.AsNoTracking()
            .Where(f => f.UserId == userId)
            .OrderBy(f => f.Slot == null).ThenBy(f => f.Slot)
            .ToListAsync(cancellationToken);

    // Deleting first and inserting after keeps the unique slot index satisfied at every step.
    public async Task ReplaceFavouritesAsync(Guid userId, IReadOnlyList<UserFavourite> favourites, CancellationToken cancellationToken)
    {
        await db.UserFavourites.Where(f => f.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        db.UserFavourites.AddRange(favourites);
    }

    public async Task<IReadOnlyList<UserHotkey>> HotkeysAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.UserHotkeys.AsNoTracking().Where(h => h.UserId == userId).OrderBy(h => h.ActionId).ToListAsync(cancellationToken);

    public async Task ReplaceHotkeysAsync(Guid userId, IReadOnlyList<UserHotkey> hotkeys, CancellationToken cancellationToken)
    {
        await db.UserHotkeys.Where(h => h.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        db.UserHotkeys.AddRange(hotkeys);
    }

    public Task<UserSettingsDocument?> SettingsAsync(Guid userId, CancellationToken cancellationToken) =>
        db.UserSettings.FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

    public void AddSettings(UserSettingsDocument settings) => db.UserSettings.Add(settings);

    public void ExpectSettingsVersion(UserSettingsDocument settings, uint version) =>
        db.Entry(settings).Property(s => s.Version).OriginalValue = version;
}
