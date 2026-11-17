using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Norma.Api.Data.Entities;

namespace Norma.Api.Data.Configurations;

/// <summary>Замеры веса: выборка всегда по пользователю и дате.</summary>
internal sealed class WeightMeasurementConfiguration : IEntityTypeConfiguration<WeightMeasurement>
{
    public void Configure(EntityTypeBuilder<WeightMeasurement> builder)
    {
        builder.ToTable("weight_measurements");

        builder.HasKey(measurement => measurement.Id);

        // В один день может быть несколько замеров (утро/вечер), поэтому пара
        // (UserId, Date) не уникальна - только индекс под выборку тренда.
        builder.HasIndex(measurement => new { measurement.UserId, measurement.Date });

        builder
            .HasOne(measurement => measurement.User)
            .WithMany()
            .HasForeignKey(measurement => measurement.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
