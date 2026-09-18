using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenVpnPilot.Server.Data.Entities;

namespace OpenVpnPilot.Server.Data.Configurations;

public sealed class ProfileContentConfiguration : IEntityTypeConfiguration<ProfileContent>
{
    public void Configure(EntityTypeBuilder<ProfileContent> builder)
    {
        builder.HasKey(c => c.ProfileId);
        builder.Property(c => c.Cipher).HasColumnType("bytea").IsRequired();
    }
}
