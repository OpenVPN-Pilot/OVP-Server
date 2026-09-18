using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenVpnPilot.Server.Data.Entities;

namespace OpenVpnPilot.Server.Data.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Username).HasColumnType("citext").HasMaxLength(256).IsRequired();
        builder.HasIndex(u => u.Username).IsUnique();
        builder.Property(u => u.DisplayName).HasMaxLength(256);
        builder.Property(u => u.ExternalId).HasMaxLength(512);
        builder.Property(u => u.Provider).HasConversion<string>().HasMaxLength(16);
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(16);
        builder.Property(u => u.State).HasConversion<string>().HasMaxLength(16);
        builder.Property(u => u.StateSource).HasConversion<string>().HasMaxLength(16);
    }
}
