using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Norma.Api.Data.Entities;

namespace Norma.Api.Data.Configurations;

/// <summary>Профиль один на пользователя: ключ - это и есть UserId.</summary>
internal sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("user_profiles");

        builder.HasKey(profile => profile.UserId);

        // Enum строкой: в базе видно "Female", а не 0.
        builder.Property(profile => profile.Sex).HasConversion<string>().HasMaxLength(10);
        builder.Property(profile => profile.Activity).HasConversion<string>().HasMaxLength(20);
        builder.Property(profile => profile.Goal).HasConversion<string>().HasMaxLength(20);

        builder
            .HasOne(profile => profile.User)
            .WithOne()
            .HasForeignKey<UserProfile>(profile => profile.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
