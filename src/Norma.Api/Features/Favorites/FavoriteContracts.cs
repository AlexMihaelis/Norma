using Norma.Api.Data.Entities;
using Norma.Api.Features.Foods;

namespace Norma.Api.Features.Favorites;

/// <summary>Избранный продукт в ответе: продукт целиком плюс когда добавлен.</summary>
public sealed record FavoriteResponse(
    Guid FoodId,
    string Name,
    FoodState State,
    FoodUnit BaseUnit,
    double KcalPer100g,
    double ProteinPer100g,
    double FatPer100g,
    double CarbsPer100g,
    DateTimeOffset AddedAt);

/// <summary>Список избранного пользователя.</summary>
public sealed record FavoriteListResponse(IReadOnlyList<FavoriteResponse> Items);

/// <summary>Тело добавления: только продукт, отметка ставится вручную.</summary>
public sealed record AddFavoriteRequest(Guid? FoodId);

/// <summary>Преобразование записи избранного в ответ.</summary>
internal static class FavoriteMapping
{
    public static FavoriteResponse ToResponse(FavoriteFood favorite, Food food) =>
        new(
            food.Id,
            FoodDisplay.PrimaryName(food),
            food.State,
            food.BaseUnit,
            food.KcalPer100g,
            food.ProteinPer100g,
            food.FatPer100g,
            food.CarbsPer100g,
            favorite.CreatedAt);
}
