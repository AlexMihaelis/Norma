using Norma.Api.Data.Entities;

namespace Norma.Api.Features.Foods;

/// <summary>
/// Показ продукта: основное название с откатом на первое доступное. Одно место на
/// поиск, дневник и избранное, чтобы подпись продукта везде совпадала.
/// </summary>
internal static class FoodDisplay
{
    public static string PrimaryName(Food food) =>
        (food.Names.FirstOrDefault(name => name.IsPrimary)
            ?? food.Names.FirstOrDefault())?.Name
        ?? "Без названия";
}
