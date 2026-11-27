using Norma.Api.Data.Entities;
using Norma.Api.Features.Foods;
using Norma.Api.Features.Images;
using Norma.Core.Nutrition;
using Norma.Core.Recipes;

namespace Norma.Api.Features.Recipes;

/// <summary>
/// Преобразование рецепта в ответ: КБЖУ и автоматические метки считаются из
/// состава, поэтому у списка и карточки одна и та же арифметика.
/// </summary>
internal static class RecipeMapping
{
    public static RecipeTotals Totals(Recipe recipe) => RecipeNutrition.Totals(recipe);

    public static RecipeSummary ToSummary(Recipe recipe)
    {
        var totals = Totals(recipe);

        return new RecipeSummary(
            recipe.Id,
            recipe.Name,
            totals.Grams,
            totals.Per100.Kcal,
            totals.Per100.Protein,
            totals.Per100.Fat,
            totals.Per100.Carbs,
            totals.Total.Kcal,
            recipe.Ingredients.Count,
            ManualTags(recipe),
            AutoTags(recipe, totals),
            recipe.IsFavorite,
            RecipeNutrition.HasManual(recipe),
            PictureData.ToDataUrl(recipe.PhotoContentType, recipe.PhotoBytes),
            recipe.CreatedAt);
    }

    public static RecipeResponse ToResponse(Recipe recipe)
    {
        var totals = Totals(recipe);

        return new RecipeResponse(
            recipe.Id,
            recipe.Name,
            recipe.Instructions,
            totals.Grams,
            totals.Per100.Kcal,
            totals.Per100.Protein,
            totals.Per100.Fat,
            totals.Per100.Carbs,
            totals.Total.Kcal,
            ManualTags(recipe),
            AutoTags(recipe, totals),
            recipe.IsFavorite,
            RecipeNutrition.HasManual(recipe),
            PictureData.ToDataUrl(recipe.PhotoContentType, recipe.PhotoBytes),
            recipe.Ingredients
                .OrderBy(ingredient => ingredient.Id)
                .Select(ToIngredient)
                .ToList(),
            recipe.CreatedAt);
    }

    private static IReadOnlyList<string> ManualTags(Recipe recipe) =>
        recipe.Tags
            .Select(tag => tag.Tag)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(tag => tag, StringComparer.Ordinal)
            .ToList();

    private static IReadOnlyList<string> AutoTags(Recipe recipe, RecipeTotals totals) =>
        RecipeTagMath
            .Detect(recipe.Ingredients.Select(IngredientName), totals.Per100)
            .Select(tag => tag.ToString())
            .OrderBy(tag => tag, StringComparer.Ordinal)
            .ToList();

    private static string IngredientName(RecipeIngredient ingredient) =>
        ingredient.Food is null ? string.Empty : FoodDisplay.PrimaryName(ingredient.Food);

    private static RecipeIngredientResponse ToIngredient(RecipeIngredient ingredient)
    {
        var food = ingredient.Food
            ?? throw new InvalidOperationException("Состав рецепта загружен без продукта.");

        return new RecipeIngredientResponse(
            food.Id,
            FoodDisplay.PrimaryName(food),
            ingredient.Grams,
            NutritionMath.ForQuantity(
                new NutritionValues(
                    food.KcalPer100g,
                    food.ProteinPer100g,
                    food.FatPer100g,
                    food.CarbsPer100g),
                ingredient.Grams).Kcal,
            food.KcalPer100g,
            food.ProteinPer100g,
            food.FatPer100g,
            food.CarbsPer100g);
    }
}
