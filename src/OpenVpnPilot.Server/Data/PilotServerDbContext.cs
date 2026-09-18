using Microsoft.EntityFrameworkCore;
using OpenVpnPilot.Server.Data.Entities;

namespace OpenVpnPilot.Server.Data;

public sealed class PilotServerDbContext(DbContextOptions<PilotServerDbContext> options) : DbContext(options)
{
    public const string ChangeSequence = "change_seq";

    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Profile> Profiles => Set<Profile>();

    public DbSet<ProfileContent> ProfileContents => Set<ProfileContent>();

    public DbSet<Tag> Tags => Set<Tag>();

    public DbSet<VaultEntry> VaultEntries => Set<VaultEntry>();

    public DbSet<Tombstone> Tombstones => Set<Tombstone>();

    public DbSet<SyncState> SyncStates => Set<SyncState>();

    public DbSet<UserFavourite> UserFavourites => Set<UserFavourite>();

    public DbSet<UserHotkey> UserHotkeys => Set<UserHotkey>();

    public DbSet<UserSettingsDocument> UserSettings => Set<UserSettingsDocument>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Names compare the way people expect them to: 'Office' and 'office' are the same tag.
        modelBuilder.HasPostgresExtension("citext");

        // Every change a client synchronises is stamped from this one sequence, which is what a cursor counts.
        modelBuilder.HasSequence<long>(ChangeSequence);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PilotServerDbContext).Assembly);
    }
}
