using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Norma.Api.Data.Entities;

namespace Norma.Api.Data.Configurations;

/// <summary>Записи активности: выборка всегда по пользователю и дню.</summary>
internal sealed class ActivityRecordConfiguration : IEntityTypeConfiguration<ActivityRecord>
{
    public void Configure(EntityTypeBuilder<ActivityRecord> builder)
    {
        builder.ToTable("activity_records");

        builder.HasKey(record => record.Id);

        // Вид активности строкой: в базе видно "Walking", а не 0.
        builder.Property(record => record.Kind).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(record => new { record.UserId, record.Date });

        builder
            .HasOne(record => record.User)
            .WithMany()
            .HasForeignKey(record => record.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
