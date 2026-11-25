using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Norma.Api.Data.Entities;

namespace Norma.Api.Data.Configurations;

/// <summary>
/// Обхваты тела: выборка по пользователю и дате, один замер вида в день.
/// </summary>
internal sealed class BodyMeasurementConfiguration : IEntityTypeConfiguration<BodyMeasurement>
{
    public void Configure(EntityTypeBuilder<BodyMeasurement> builder)
    {
        builder.ToTable("body_measurements");

        builder.HasKey(measurement => measurement.Id);

        builder.Property(measurement => measurement.Kind).HasMaxLength(32);

        // Повторный замер того же вида в тот же день - это исправление, а не дубль.
        builder.HasIndex(measurement => new { measurement.UserId, measurement.Date, measurement.Kind })
            .IsUnique();

        builder
            .HasOne(measurement => measurement.User)
            .WithMany()
            .HasForeignKey(measurement => measurement.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}