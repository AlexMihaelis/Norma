using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Norma.Api.Data.Entities;

namespace Norma.Api.Data.Configurations;

/// <summary>Снимки профиля: выборка всегда по пользователю и дате действия.</summary>
internal sealed class UserProfileSnapshotConfiguration : IEntityTypeConfiguration<UserProfileSnapshot>
{
    public void Configure(EntityTypeBuilder<UserProfileSnapshot> builder)
    {
        builder.ToTable("user_profile_snapshots");

        builder.HasKey(snapshot => snapshot.Id);

        // Enum строкой: в базе видно "Female", а не 0.
        builder.Property(snapshot => snapshot.Sex).HasConversion<string>().HasMaxLength(10);
        builder.Property(snapshot => snapshot.Activity).HasConversion<string>().HasMaxLength(20);
        builder.Property(snapshot => snapshot.Goal).HasConversion<string>().HasMaxLength(20);

        // По этой паре ищется снимок, действующий на день статистики.
        builder.HasIndex(snapshot => new { snapshot.UserId, snapshot.EffectiveFrom });

        builder
            .HasOne(snapshot => snapshot.User)
            .WithMany()
            .HasForeignKey(snapshot => snapshot.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
