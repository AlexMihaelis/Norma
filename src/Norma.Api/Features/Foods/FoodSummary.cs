using Norma.Api.Data.Entities;

namespace Norma.Api.Features.Foods;

/// <summary>
/// Продукт в ответе API: одно представление для поиска и создания, чтобы клиент
/// разбирал одинаковую форму в обоих случаях.
/// </summary>
public sealed record FoodSummary(
    Guid Id,
    string Name,
    FoodState State,
    FoodUnit BaseUnit,
    double KcalPer100g,
    double ProteinPer100g,
    double FatPer100g,
    double CarbsPer100g)
{
    public static FoodSummary FromFood(Food food) =>
        new(
            food.Id,
            FoodDisplay.PrimaryName(food),
            food.State,
            food.BaseUnit,
            food.KcalPer100g,
            food.ProteinPer100g,
            food.FatPer100g,
            food.CarbsPer100g);
}
