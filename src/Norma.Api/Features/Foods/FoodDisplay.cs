using Norma.Api.Data.Entities;

namespace Norma.Api.Features.Foods;

/// <summary>
/// Основное название продукта с откатом на первое доступное: одно место на поиск,
/// дневник и избранное.
/// </summary>
internal static class FoodDisplay
{
    public static string PrimaryName(Food food) =>
        (food.Names.FirstOrDefault(name => name.IsPrimary)
            ?? food.Names.FirstOrDefault())?.Name
        ?? "Без названия";
}
