using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Norma.Api.Data;
using Norma.Api.Data.Entities;

namespace Norma.Api.Features.Foods.SearchFoods;

/// <summary>
/// Срез «поиск продукта». Ищет по русским названиям и синонимам, отдаёт КБЖУ на
/// 100 г и состояние продукта.
/// </summary>
/// <remarks>
/// Поиск идёт по локальной базе: это быстро и не зависит от доступности Open Food
/// Facts. Бренды и штрихкоды появятся отдельным срезом, когда база пополнится.
/// </remarks>
public static class SearchFoodsEndpoint
{
    private const int DefaultLimit = 20;
    private const int MaxLimit = 50;
    private const int MinQueryLength = 2;

    public static IEndpointRouteBuilder MapSearchFoods(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet("/foods/search", Handle)
            .WithName("SearchFoods");

        return endpoints;
    }

    private static async Task<Results<Ok<FoodSearchResponse>, ValidationProblem>> Handle(
        string? q,
        int? limit,
        NormaDbContext db,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        var query = q?.Trim();

        if (string.IsNullOrWhiteSpace(query) || query.Length < MinQueryLength)
        {
            errors["q"] = [$"Поисковый запрос должен быть не короче {MinQueryLength} символов."];
        }

        var take = limit ?? DefaultLimit;

        if (take is < 1 or > MaxLimit)
        {
            errors["limit"] = [$"limit должен быть в диапазоне 1-{MaxLimit}."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var pattern = $"%{EscapeLike(query!)}%";

        // Один запрос за идентификаторами: совпадение по названию ИЛИ по синониму.
        var foodIds = await db.FoodNames
            .Where(name => EF.Functions.ILike(name.Name, pattern))
            .Select(name => name.FoodId)
            .Union(
                db.FoodAliases
                    .Where(alias => EF.Functions.ILike(alias.Alias, pattern))
                    .Select(alias => alias.FoodId))
            .ToListAsync(cancellationToken);

        if (foodIds.Count == 0)
        {
            return TypedResults.Ok(new FoodSearchResponse([]));
        }

        var foods = await db.Foods
            .Where(food => foodIds.Contains(food.Id))
            .Include(food => food.Names)
            .ToListAsync(cancellationToken);

        var items = foods
            .Select(ToItem)
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Take(take)
            .ToList();

        return TypedResults.Ok(new FoodSearchResponse(items));
    }

    private static FoodSearchItem ToItem(Food food)
    {
        var primary = food.Names.FirstOrDefault(name => name.IsPrimary)
            ?? food.Names.FirstOrDefault();

        return new FoodSearchItem(
            Id: food.Id,
            Name: primary?.Name ?? "Без названия",
            State: food.State,
            KcalPer100g: food.KcalPer100g,
            ProteinPer100g: food.ProteinPer100g,
            FatPer100g: food.FatPer100g,
            CarbsPer100g: food.CarbsPer100g);
    }

    /// <summary>
    /// Экранирует служебные символы LIKE, чтобы запрос «50%» искал именно «50%»,
    /// а не всё подряд.
    /// </summary>
    private static string EscapeLike(string value) =>
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}

/// <summary>Ответ поиска: продукты с КБЖУ на 100 г.</summary>
public sealed record FoodSearchResponse(IReadOnlyList<FoodSearchItem> Items);

/// <summary>Найденный продукт.</summary>
public sealed record FoodSearchItem(
    Guid Id,
    string Name,
    FoodState State,
    double KcalPer100g,
    double ProteinPer100g,
    double FatPer100g,
    double CarbsPer100g);
