using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Norma.Api.Data.Entities;

namespace Norma.Api.Data.Configurations;

/// <summary>Приемы воды: выборка всегда по пользователю и дню.</summary>
internal sealed class WaterIntakeConfiguration : IEntityTypeConfiguration<WaterIntake>
{
    public void Configure(EntityTypeBuilder<WaterIntake> builder)
    {
        builder.ToTable("water_intakes");

        builder.HasKey(intake => intake.Id);

        // За день приемов может быть много, поэтому (UserId, Date) - только
        // индекс под выборку дня, без уникальности.
        builder.HasIndex(intake => new { intake.UserId, intake.Date });

        builder
            .HasOne(intake => intake.User)
            .WithMany()
            .HasForeignKey(intake => intake.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
