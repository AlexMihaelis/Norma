using Norma.Api.Data.Entities;
using Norma.Api.Features.Foods;
using Norma.Api.Features.Recipes;
using Norma.Core.Nutrition;

namespace Norma.Api.Features.Diary;

/// <summary>
/// Пищевая ценность позиции дневника: у продукта - из каталога, у рецепта - из
/// состава. Одна точка расчета для дня, повтора и статистики.
/// </summary>
internal static class MealItemNutrition
{
    public static NutritionValues Per100(MealItem item)
    {
        if (item.Food is not null)
        {
            return new NutritionValues(
                item.Food.KcalPer100g,
                item.Food.ProteinPer100g,
                item.Food.FatPer100g,
                item.Food.CarbsPer100g);
        }

        if (item.Recipe is not null)
        {
            return RecipeNutrition.Per100(item.Recipe);
        }

        throw new InvalidOperationException("Позиция дневника без продукта и без рецепта.");
    }

    public static NutritionValues Values(MealItem item) =>
        NutritionMath.ForQuantity(Per100(item), item.Quantity);

    /// <summary>Название для экрана: имя продукта или название рецепта.</summary>
    public static string Name(MealItem item)
    {
        if (item.Food is not null)
        {
            return FoodDisplay.PrimaryName(item.Food);
        }

        return item.Recipe?.Name
            ?? throw new InvalidOperationException("Позиция дневника без продукта и без рецепта.");
    }
}
