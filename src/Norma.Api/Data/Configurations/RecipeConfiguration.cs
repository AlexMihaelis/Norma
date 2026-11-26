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

        builder.Property(recipe => recipe.Note).HasMaxLength(500);

        // Список всегда «сначала новые» для конкретного пользователя.
        builder.HasIndex(recipe => new { recipe.UserId, recipe.CreatedAt });

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
