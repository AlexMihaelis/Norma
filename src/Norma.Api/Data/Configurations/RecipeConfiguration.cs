using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Norma.Api.Data.Entities;

namespace Norma.Api.Data.Configurations;

/// <summary>Рецепты: принадлежат пользователю, удаляются вместе с ним.</summary>
internal sealed class RecipeConfiguration : IEntityTypeConfiguration<Recipe>
{
    public void Configure(EntityTypeBuilder<Recipe> builder)
    {
        builder.ToTable("recipes");

        builder.HasKey(recipe => recipe.Id);

        builder.Property(recipe => recipe.Name).HasMaxLength(100).IsRequired();

        builder.Property(recipe => recipe.Instructions).HasMaxLength(2000);

        builder.Property(recipe => recipe.PhotoContentType).HasMaxLength(50);

        // Список всегда «сначала новые» для конкретного пользователя.
        builder.HasIndex(recipe => new { recipe.UserId, recipe.CreatedAt });

        // Избранное показывается отдельным списком, поэтому фильтр по флагу.
        builder.HasIndex(recipe => new { recipe.UserId, recipe.IsFavorite });

        builder
            .HasOne(recipe => recipe.User)
            .WithMany()
            .HasForeignKey(recipe => recipe.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Состав рецепта: один продукт входит в рецепт один раз.</summary>
internal sealed class RecipeIngredientConfiguration : IEntityTypeConfiguration<RecipeIngredient>
{
    public void Configure(EntityTypeBuilder<RecipeIngredient> builder)
    {
        builder.ToTable("recipe_ingredients");

        builder.HasKey(ingredient => ingredient.Id);

        // Дубль продукта в рецепте - это ошибка ввода, а не разные строки.
        builder.HasIndex(ingredient => new { ingredient.RecipeId, ingredient.FoodId }).IsUnique();

        builder
            .HasOne(ingredient => ingredient.Recipe)
            .WithMany(recipe => recipe.Ingredients)
            .HasForeignKey(ingredient => ingredient.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict как в дневнике: без продукта строка рецепта теряет ценность.
        builder
            .HasOne(ingredient => ingredient.Food)
            .WithMany()
            .HasForeignKey(ingredient => ingredient.FoodId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Метки рецепта: одна метка входит в рецепт один раз.</summary>
internal sealed class RecipeTagLinkConfiguration : IEntityTypeConfiguration<RecipeTagLink>
{
    public void Configure(EntityTypeBuilder<RecipeTagLink> builder)
    {
        builder.ToTable("recipe_tags");

        builder.HasKey(tag => tag.Id);

        builder.Property(tag => tag.Tag).HasMaxLength(30).IsRequired();

        builder.HasIndex(tag => new { tag.RecipeId, tag.Tag }).IsUnique();

        // Метки - часть рецепта: удалили рецепт, ушли и они.
        builder
            .HasOne(tag => tag.Recipe)
            .WithMany(recipe => recipe.Tags)
            .HasForeignKey(tag => tag.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
