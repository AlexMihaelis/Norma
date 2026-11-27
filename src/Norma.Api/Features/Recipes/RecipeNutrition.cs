using Norma.Api.Data.Entities;
using Norma.Core.Nutrition;
using Norma.Core.Recipes;

namespace Norma.Api.Features.Recipes;

/// <summary>
/// КБЖУ рецепта: свои цифры, если человек их ввел, иначе расчет из состава.
/// Одна точка для карточки рецепта, списка и позиции дневника.
/// </summary>
/// <remarks>
/// Состав должен быть загружен вместе с продуктами: без них цифры не посчитать.
/// </remarks>
internal static class RecipeNutrition
{
    /// <summary>Заданы ли свои КБЖУ: четыре поля либо все, либо ни одного.</summary>
    public static bool HasManual(Recipe recipe) =>
        recipe.ManualKcalPer100g is not null
        && recipe.ManualProteinPer100g is not null
        && recipe.ManualFatPer100g is not null
        && recipe.ManualCarbsPer100g is not null;

    public static NutritionValues Per100(Recipe recipe)
    {
        var computed = RecipeMath.Compute(recipe.Ingredients.Select(Line));

        return HasManual(recipe) ? Manual(recipe) : computed.Per100;
    }

    public static RecipeTotals Totals(Recipe recipe)
    {
        var computed = RecipeMath.Compute(recipe.Ingredients.Select(Line));
        var per100 = HasManual(recipe) ? Manual(recipe) : computed.Per100;

        // Вес всегда из состава, а калории целиком - по действующим на 100 г.
        return new RecipeTotals(
            computed.Grams,
            NutritionMath.ForQuantity(per100, computed.Grams),
            per100);
    }

    private static NutritionValues Manual(Recipe recipe) =>
        new(
            recipe.ManualKcalPer100g!.Value,
            recipe.ManualProteinPer100g!.Value,
            recipe.ManualFatPer100g!.Value,
            recipe.ManualCarbsPer100g!.Value);

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
