using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenVpnPilot.Server.Data.Entities;

namespace OpenVpnPilot.Server.Data.Configurations;

public sealed class UserFavouriteConfiguration : IEntityTypeConfiguration<UserFavourite>
{
    public void Configure(EntityTypeBuilder<UserFavourite> builder)
    {
        builder.HasKey(f => new { f.UserId, f.ProfileId });
        builder.HasIndex(f => new { f.UserId, f.Slot }).IsUnique().HasFilter("slot IS NOT NULL");
        builder.HasOne<User>().WithMany().HasForeignKey(f => f.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Profile>().WithMany().HasForeignKey(f => f.ProfileId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class UserHotkeyConfiguration : IEntityTypeConfiguration<UserHotkey>
{
    public void Configure(EntityTypeBuilder<UserHotkey> builder)
    {
        builder.HasKey(h => new { h.UserId, h.ActionId });
        builder.Property(h => h.ActionId).HasMaxLength(100);
        builder.Property(h => h.Gesture).HasMaxLength(100);
        builder.HasOne<User>().WithMany().HasForeignKey(h => h.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Profile>().WithMany().HasForeignKey(h => h.ProfileId).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class UserSettingsDocumentConfiguration : IEntityTypeConfiguration<UserSettingsDocument>
{
    public void Configure(EntityTypeBuilder<UserSettingsDocument> builder)
    {
        builder.ToTable("user_settings");
        builder.HasKey(s => s.UserId);
        builder.Property(s => s.Document).HasColumnType("jsonb").IsRequired();
        builder.Property(s => s.Version).IsRowVersion();
        builder.HasOne<User>().WithOne().HasForeignKey<UserSettingsDocument>(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
