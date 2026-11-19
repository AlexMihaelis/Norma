using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Norma.Api.Data;
using Norma.Api.Data.Entities;
using Norma.Api.Features.Auth;

namespace Norma.Api.Features.Foods.CreateFood;

/// <summary>
/// Срез «свой продукт»: продукт попадает в общий каталог, авторство хранится
/// отдельно - продукт должен находиться поиском у всех.
/// </summary>
/// <remarks>
/// Валидация строгая: дневник пересчитывает КБЖУ из продукта, поэтому ошибка в
/// каталоге меняет цифры у всех.
/// </remarks>
public static class CreateFoodEndpoint
{
    private const int MinNameLength = 2;
    private const int MaxNameLength = 100;
    private const int MaxAliases = 5;

    // Потолок на 100 г, а не на порцию: чистый жир дает 900 ккал на 100 г, а
    // 950 оставляет запас для плотных жиров из источников (смалец - 902).
    private const double MaxKcalPer100 = 950;

    private const double MaxMacroPer100 = 100;

    // Сто граммов продукта не могут содержать заметно больше 100 г макронутриентов,
    // но 100.0 с копейками после округления источника - норма.
    private const double MacroSumTolerance = 0.5;

    public static IEndpointRouteBuilder MapCreateFood(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost("/foods", Handle)
            .WithName("CreateFood")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<Results<Created<FoodSummary>, ValidationProblem, UnauthorizedHttpResult>> Handle(
        CreateFoodRequest? request,
        ClaimsPrincipal principal,
        NormaDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();

        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        var name = request?.Name?.Trim();

        if (string.IsNullOrWhiteSpace(name) || name.Length < MinNameLength || name.Length > MaxNameLength)
        {
            errors["name"] = [$"Название должно быть длиной от {MinNameLength} до {MaxNameLength} символов."];
        }

        var state = ParseState(request?.State, errors);
        var baseUnit = ParseUnit(request?.BaseUnit, errors);

        ValidateMacro(errors, "kcalPer100", request?.KcalPer100, MaxKcalPer100);
        ValidateMacro(errors, "proteinPer100", request?.ProteinPer100, MaxMacroPer100);
        ValidateMacro(errors, "fatPer100", request?.FatPer100, MaxMacroPer100);
        ValidateMacro(errors, "carbsPer100", request?.CarbsPer100, MaxMacroPer100);

        var macroSum = (request?.ProteinPer100 ?? 0) + (request?.FatPer100 ?? 0) + (request?.CarbsPer100 ?? 0);

        if (macroSum > MaxMacroPer100 + MacroSumTolerance)
        {
            errors["macros"] = ["Сумма белков, жиров и углеводов не может превышать 100 г на 100 г продукта."];
        }

        var aliases = NormalizeAliases(request?.Aliases, errors);

        // Два «Молока» с разными КБЖУ не различить на экране, поэтому точное
        // совпадение без регистра отправляем к поиску.
        if (name is not null && !errors.ContainsKey("name"))
        {
            var duplicate = await db.FoodNames
                .AnyAsync(
                    existing => existing.Locale == "ru"
                        && existing.Name.ToLower() == name.ToLower(),
                    cancellationToken);

            if (duplicate)
            {
                errors["name"] =
                [
                    "Продукт с таким названием уже есть в каталоге. Найдите его через поиск или уточните название.",
                ];
            }
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var food = new Food
        {
            Id = Guid.NewGuid(),
            Source = FoodSource.User,
            SourceId = null,
            State = state!.Value,
            BaseUnit = baseUnit!.Value,
            CreatedByUserId = userId.Value,
            KcalPer100g = request!.KcalPer100!.Value,
            ProteinPer100g = request.ProteinPer100!.Value,
            FatPer100g = request.FatPer100!.Value,
            CarbsPer100g = request.CarbsPer100!.Value,
        };

        food.Names.Add(new FoodName
        {
            Id = Guid.NewGuid(),
            Locale = "ru",
            Name = name!,
            IsPrimary = true,
        });

        foreach (var alias in aliases)
        {
            food.Aliases.Add(new FoodAlias { Id = Guid.NewGuid(), Alias = alias });
        }

        db.Foods.Add(food);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Created($"/api/v1/foods/{food.Id}", FoodSummary.FromFood(food));
    }

    private static void ValidateMacro(
        Dictionary<string, string[]> errors,
        string key,
        double? value,
        double max)
    {
        if (value is not double number || double.IsNaN(number) || number < 0 || number > max)
        {
            errors[key] = [$"Значение должно быть в диапазоне 0-{max}."];
        }
    }

    private static List<string> NormalizeAliases(IReadOnlyList<string>? raw, Dictionary<string, string[]> errors)
    {
        var aliases = new List<string>();

        if (raw is null || raw.Count == 0)
        {
            return aliases;
        }

        if (raw.Count > MaxAliases)
        {
            errors["aliases"] = [$"Синонимов может быть не больше {MaxAliases}."];
            return aliases;
        }

        foreach (var alias in raw)
        {
            var trimmed = alias?.Trim();

            if (string.IsNullOrWhiteSpace(trimmed)
                || trimmed.Length < MinNameLength
                || trimmed.Length > MaxNameLength)
            {
                errors["aliases"] = [$"Каждый синоним должен быть длиной от {MinNameLength} до {MaxNameLength} символов."];
                return aliases;
            }

            if (!aliases.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
            {
                aliases.Add(trimmed);
            }
        }

        return aliases;
    }

    private static FoodState? ParseState(string? raw, Dictionary<string, string[]> errors)
    {
        // Состояние необязательно: не знаешь, сырой продукт или готовый - Unknown.
        if (string.IsNullOrWhiteSpace(raw))
        {
            return FoodState.Unknown;
        }

        if (!Enum.TryParse<FoodState>(raw, ignoreCase: true, out var state) || !Enum.IsDefined(state))
        {
            errors["state"] = [$"Недопустимое состояние: ожидается {string.Join(", ", Enum.GetNames<FoodState>())}."];
            return null;
        }

        return state;
    }

    private static FoodUnit? ParseUnit(string? raw, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return FoodUnit.Gram;
        }

        if (!Enum.TryParse<FoodUnit>(raw, ignoreCase: true, out var unit) || !Enum.IsDefined(unit))
        {
            errors["baseUnit"] = [$"Недопустимая единица: ожидается {string.Join(", ", Enum.GetNames<FoodUnit>())}."];
            return null;
        }

        return unit;
    }
}
