using Norma.Api.Data.Entities;
using Norma.Api.Features.Foods;
using Norma.Api.Features.Recipes;
using Norma.Core.Nutrition;

namespace Norma.Api.Features.Diary;

/// <summary>
/// Преобразование позиции в контракт ответа: КБЖУ считается из текущего продукта
/// или состава рецепта, поэтому правка каталога меняет и историю дневника.
/// </summary>
internal static class DiaryMapping
{
    /// <summary>Позиция с загруженным продуктом или рецептом (чтение дня).</summary>
    public static DiaryEntryResponse ToEntry(MealItem item) =>
        Build(
            item.Id,
            item.FoodId,
            item.RecipeId,
            MealItemNutrition.Name(item),
            item.Quantity,
            item.Unit,
            MealItemNutrition.Values(item));

    /// <summary>Только что записанная позиция-продукт.</summary>
    public static DiaryEntryResponse ToFoodEntry(Guid id, Food food, double quantity, FoodUnit unit) =>
        Build(
            id,
            food.Id,
            null,
            FoodDisplay.PrimaryName(food),
            quantity,
            unit,
            NutritionMath.ForQuantity(
                new NutritionValues(
                    food.KcalPer100g,
                    food.ProteinPer100g,
                    food.FatPer100g,
                    food.CarbsPer100g),
                quantity));

    /// <summary>Только что записанная позиция-рецепт: состав должен быть загружен.</summary>
    public static DiaryEntryResponse ToRecipeEntry(Guid id, Recipe recipe, double quantity) =>
        Build(
            id,
            null,
            recipe.Id,
            recipe.Name,
            quantity,
            FoodUnit.Gram,
            NutritionMath.ForQuantity(RecipeNutrition.Per100(recipe), quantity));

    /// <summary>Итоги по округленным позициям: сумма строк на экране совпадет с итогом.</summary>
    public static DiaryTotalsResponse Totals(IEnumerable<DiaryEntryResponse> entries)
    {
        var items = entries.ToList();

        return new DiaryTotalsResponse(
            items.Sum(item => item.Kcal),
            Math.Round(items.Sum(item => item.Protein), 1, MidpointRounding.AwayFromZero),
            Math.Round(items.Sum(item => item.Fat), 1, MidpointRounding.AwayFromZero),
            Math.Round(items.Sum(item => item.Carbs), 1, MidpointRounding.AwayFromZero));
    }

    /// <summary>Целое число калорий: половина округляется вверх (247.5 -&gt; 248).</summary>
    private static DiaryEntryResponse Build(
        Guid id,
        Guid? foodId,
        Guid? recipeId,
        string name,
        double quantity,
        FoodUnit unit,
        NutritionValues values) =>
        new(
            id,
            recipeId is null ? DiaryItemKind.Food : DiaryItemKind.Recipe,
            foodId,
            recipeId,
            name,
            quantity,
            unit,
            (int)Math.Round(values.Kcal, MidpointRounding.AwayFromZero),
            Math.Round(values.Protein, 1, MidpointRounding.AwayFromZero),
            Math.Round(values.Fat, 1, MidpointRounding.AwayFromZero),
            Math.Round(values.Carbs, 1, MidpointRounding.AwayFromZero));
}
