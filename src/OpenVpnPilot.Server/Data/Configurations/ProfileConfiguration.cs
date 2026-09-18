using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenVpnPilot.Server.Data.Entities;

namespace OpenVpnPilot.Server.Data.Configurations;

public sealed class ProfileConfiguration : IEntityTypeConfiguration<Profile>
{
    public void Configure(EntityTypeBuilder<Profile> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.RemoteHost).HasMaxLength(255);
        builder.Property(p => p.Protocol).HasMaxLength(16);
        builder.Property(p => p.Notes).HasMaxLength(4000);
        builder.Property(p => p.Colour).HasMaxLength(9);
        builder.Property(p => p.ContentHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(p => p.ContentHash).IsUnique();
        builder.HasIndex(p => p.ChangeSeq);
        builder.Property(p => p.CreatedBy).HasMaxLength(256);
        builder.Property(p => p.UpdatedBy).HasMaxLength(256);

        // Maps to PostgreSQL's xmin, which changes with every update and so serves as the ETag.
        builder.Property(p => p.Version).IsRowVersion();

        builder.HasOne(p => p.Content)
            .WithOne()
            .HasForeignKey<ProfileContent>(c => c.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Tags)
            .WithMany(t => t.Profiles)
            .UsingEntity("profile_tags");

        builder.HasMany(p => p.VaultEntries)
            .WithOne(v => v.Profile)
            .HasForeignKey(v => v.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
