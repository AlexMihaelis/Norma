using Norma.Api.Data.Entities;
using Norma.Api.Features.Foods;
using Norma.Core.Nutrition;
using Norma.Core.Recipes;

namespace Norma.Api.Features.Recipes;

/// <summary>
/// Преобразование рецепта в ответ: КБЖУ считается из состава, поэтому у списка и
/// карточки одна и та же арифметика из <see cref="RecipeMath"/>.
/// </summary>
internal static class RecipeMapping
{
    public static RecipeTotals Totals(Recipe recipe) =>
        RecipeMath.Compute(recipe.Ingredients.Select(Line));

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
            recipe.CreatedAt);
    }

    public static RecipeResponse ToResponse(Recipe recipe)
    {
        var totals = Totals(recipe);

        return new RecipeResponse(
            recipe.Id,
            recipe.Name,
            recipe.Note,
            totals.Grams,
            totals.Per100.Kcal,
            totals.Per100.Protein,
            totals.Per100.Fat,
            totals.Per100.Carbs,
            totals.Total.Kcal,
            recipe.Ingredients
                .OrderBy(ingredient => ingredient.Id)
                .Select(ToIngredient)
                .ToList(),
            recipe.CreatedAt);
    }

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
                ingredient.Grams).Kcal);
    }

    private static RecipeLine Line(RecipeIngredient ingredient)
    {
        var food = ingredient.Food
            ?? throw new InvalidOperationException("Состав рецепта загружен без продукта.");

        return new RecipeLine(
            new NutritionValues(
                food.KcalPer100g,
                food.ProteinPer100g,
                food.FatPer100g,
                food.CarbsPer100g),
            ingredient.Grams);
    }
}
