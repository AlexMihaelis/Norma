using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Norma.Api.Data.Entities;

namespace Norma.Api.Data.Configurations;

/// <summary>Таблица продуктов: КБЖУ на 100 г и связь с названиями.</summary>
internal sealed class FoodConfiguration : IEntityTypeConfiguration<Food>
{
    public void Configure(EntityTypeBuilder<Food> builder)
    {
        builder.ToTable("foods");

        builder.HasKey(food => food.Id);

        // Enum строкой: в базе видно "Cooked", а не 2, и порядок значений можно
        // менять, не ломая строки.
        builder.Property(food => food.Source).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(food => food.State).HasConversion<string>().HasMaxLength(20).IsRequired();

        // У существующих продуктов миграция проставит "Gram": миллилитры появятся
        // позже.
        builder
            .Property(food => food.BaseUnit)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(FoodUnit.Gram)
            .IsRequired();

        builder.Property(food => food.SourceId).HasMaxLength(64);

        // SET NULL, а не каскад: продукт - общее знание, удаление аккаунта не должно
        // чистить каталог. Факты питания (дневник) при этом удаляются.
        builder
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(food => food.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Один и тот же продукт источника не должен импортироваться дважды.
        builder
            .HasIndex(food => new { food.Source, food.SourceId })
            .IsUnique()
            .HasFilter("\"SourceId\" IS NOT NULL");
    }
}

/// <summary>Названия: одно основное на локаль, плюс возможные варианты.</summary>
internal sealed class FoodNameConfiguration : IEntityTypeConfiguration<FoodName>
{
    public void Configure(EntityTypeBuilder<FoodName> builder)
    {
        builder.ToTable("food_names");

        builder.HasKey(name => name.Id);

        builder.Property(name => name.Locale).HasMaxLength(8).IsRequired();
        builder.Property(name => name.Name).HasMaxLength(200).IsRequired();

        builder.HasIndex(name => new { name.FoodId, name.Locale, name.Name }).IsUnique();

        builder
            .HasOne(name => name.Food)
            .WithMany(food => food.Names)
            .HasForeignKey(name => name.FoodId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Синонимы для поиска.</summary>
internal sealed class FoodAliasConfiguration : IEntityTypeConfiguration<FoodAlias>
{
    public void Configure(EntityTypeBuilder<FoodAlias> builder)
    {
        builder.ToTable("food_aliases");

        builder.HasKey(alias => alias.Id);

        builder.Property(alias => alias.Alias).HasMaxLength(200).IsRequired();

        builder.HasIndex(alias => new { alias.FoodId, alias.Alias }).IsUnique();

        builder
            .HasOne(alias => alias.Food)
            .WithMany(food => food.Aliases)
            .HasForeignKey(alias => alias.FoodId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
