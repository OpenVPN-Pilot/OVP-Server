using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenVpnPilot.Server.Data.Entities;

namespace OpenVpnPilot.Server.Data.Configurations;

public sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).HasColumnType("citext").HasMaxLength(100).IsRequired();
        builder.HasIndex(t => t.Name).IsUnique();
        builder.Property(t => t.Colour).HasMaxLength(9);
        builder.HasIndex(t => t.ChangeSeq);
    }
}
