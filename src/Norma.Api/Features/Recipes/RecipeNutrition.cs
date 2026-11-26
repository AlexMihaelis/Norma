using Norma.Api.Data.Entities;
using Norma.Core.Nutrition;
using Norma.Core.Recipes;

namespace Norma.Api.Features.Recipes;

/// <summary>
/// КБЖУ рецепта из состава: одна точка расчета для карточки рецепта, списка и
/// позиции дневника.
/// </summary>
/// <remarks>
/// Состав должен быть загружен вместе с продуктами: без них цифры не посчитать.
/// </remarks>
internal static class RecipeNutrition
{
    public static RecipeTotals Totals(Recipe recipe) =>
        RecipeMath.Compute(recipe.Ingredients.Select(Line));

    public static NutritionValues Per100(Recipe recipe) => Totals(recipe).Per100;

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
