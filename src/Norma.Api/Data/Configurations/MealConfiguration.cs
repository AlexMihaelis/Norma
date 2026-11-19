using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Norma.Api.Data.Entities;

namespace Norma.Api.Data.Configurations;

/// <summary>Приемы пищи: уникальны по пользователю, дню и типу.</summary>
internal sealed class MealConfiguration : IEntityTypeConfiguration<Meal>
{
    public void Configure(EntityTypeBuilder<Meal> builder)
    {
        builder.ToTable("meals");

        builder.HasKey(meal => meal.Id);

        // Тип строкой: в базе видно "Breakfast", а не 0.
        builder.Property(meal => meal.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();

        // Один прием каждого типа в день: второй завтрак - это утренний перекус.
        builder.HasIndex(meal => new { meal.UserId, meal.Date, meal.Kind }).IsUnique();

        builder
            .HasOne(meal => meal.User)
            .WithMany()
            .HasForeignKey(meal => meal.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Позиции приема пищи: продукт, количество и единица.</summary>
internal sealed class MealItemConfiguration : IEntityTypeConfiguration<MealItem>
{
    public void Configure(EntityTypeBuilder<MealItem> builder)
    {
        builder.ToTable("meal_items");

        builder.HasKey(item => item.Id);

        builder.Property(item => item.Unit).HasConversion<string>().HasMaxLength(20).IsRequired();

        // Позиции показываются в порядке добавления.
        builder.HasIndex(item => new { item.MealId, item.CreatedAt });

        builder
            .HasOne(item => item.Meal)
            .WithMany(meal => meal.Items)
            .HasForeignKey(item => item.MealId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict, а не каскад: без продукта позиция теряет пищевую ценность, а
        // дневник пересчитывает КБЖУ из продукта.
        builder
            .HasOne(item => item.Food)
            .WithMany()
            .HasForeignKey(item => item.FoodId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
