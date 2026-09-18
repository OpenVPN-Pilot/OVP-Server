using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenVpnPilot.Server.Data.Entities;

namespace OpenVpnPilot.Server.Data.Configurations;

public sealed class TombstoneConfiguration : IEntityTypeConfiguration<Tombstone>
{
    public void Configure(EntityTypeBuilder<Tombstone> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Kind).HasConversion<string>().HasMaxLength(16);
        builder.Property(t => t.Realm).HasMaxLength(200);
        builder.HasIndex(t => t.ChangeSeq);
    }
}

public sealed class SyncStateConfiguration : IEntityTypeConfiguration<SyncState>
{
    public void Configure(EntityTypeBuilder<SyncState> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.HasData(new SyncState { Id = 1, PrunedThrough = 0 });
    }
}
