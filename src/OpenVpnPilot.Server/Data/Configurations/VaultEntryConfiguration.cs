using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenVpnPilot.Server.Data.Entities;

namespace OpenVpnPilot.Server.Data.Configurations;

public sealed class VaultEntryConfiguration : IEntityTypeConfiguration<VaultEntry>
{
    public void Configure(EntityTypeBuilder<VaultEntry> builder)
    {
        builder.HasKey(v => new { v.ProfileId, v.Realm });
        builder.Property(v => v.Realm).HasMaxLength(200);
        builder.Property(v => v.Cipher).HasColumnType("bytea").IsRequired();
        builder.Property(v => v.CreatedBy).HasMaxLength(256);
        builder.Property(v => v.UpdatedBy).HasMaxLength(256);
        builder.HasIndex(v => v.ChangeSeq);
    }
}
