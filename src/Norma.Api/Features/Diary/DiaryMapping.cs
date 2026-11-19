using Norma.Api.Data.Entities;
using Norma.Api.Features.Foods;
using Norma.Core.Nutrition;

namespace Norma.Api.Features.Diary;

/// <summary>
/// Преобразование позиции в контракт ответа: КБЖУ считается из текущего продукта,
/// поэтому правка каталога меняет и историю дневника.
/// </summary>
internal static class DiaryMapping
{
    /// <summary>Позиция с загруженным продуктом (чтение дня).</summary>
    public static DiaryEntryResponse ToEntry(MealItem item) =>
        ToEntry(
            item.Id,
            item.Food ?? throw new InvalidOperationException("Для позиции дневника не загружен продукт."),
            item.Quantity,
            item.Unit);

    /// <summary>Целое число калорий: половина округляется вверх (247.5 -&gt; 248).</summary>
    public static DiaryEntryResponse ToEntry(Guid id, Food food, double quantity, FoodUnit unit)
    {
        var per100 = new NutritionValues(
            food.KcalPer100g,
            food.ProteinPer100g,
            food.FatPer100g,
            food.CarbsPer100g);

        var values = NutritionMath.ForQuantity(per100, quantity);

        return new DiaryEntryResponse(
            id,
            food.Id,
            FoodDisplay.PrimaryName(food),
            quantity,
            unit,
            (int)Math.Round(values.Kcal, MidpointRounding.AwayFromZero),
            Math.Round(values.Protein, 1, MidpointRounding.AwayFromZero),
            Math.Round(values.Fat, 1, MidpointRounding.AwayFromZero),
            Math.Round(values.Carbs, 1, MidpointRounding.AwayFromZero));
    }

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
}
