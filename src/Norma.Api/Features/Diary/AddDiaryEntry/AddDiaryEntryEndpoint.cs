using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Norma.Api.Data;
using Norma.Api.Data.Entities;
using Norma.Api.Features.Auth;

namespace Norma.Api.Features.Diary.AddDiaryEntry;

/// <summary>
/// Срез «записать съеденное»: находит или создает прием пищи на дату и кладет в
/// него позицию. КБЖУ не сохраняется - оно считается из продукта при чтении.
/// </summary>
/// <remarks>
/// Прием пищи создается лениво: пользователь не заполняет завтрак, обед и ужин
/// заранее, поэтому строка <see cref="Meal"/> появляется вместе с первой позицией.
/// </remarks>
public static class AddDiaryEntryEndpoint
{
    /// <summary>Минимум и максимум количества в базовой единице продукта.</summary>
    private const double MinQuantity = 0.1;
    private const double MaxQuantity = 5000;

    public static IEndpointRouteBuilder MapAddDiaryEntry(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost("/diary", Handle)
            .WithName("AddDiaryEntry")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<Results<Created<DiaryEntryResponse>, ValidationProblem, UnauthorizedHttpResult>> Handle(
        AddDiaryEntryRequest? request,
        ClaimsPrincipal principal,
        NormaDbContext db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();

        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        var kind = ParseMealKind(request?.Meal, errors);
        var requestedUnit = ParseUnit(request?.Unit, errors);

        if (request?.FoodId is null || request.FoodId == Guid.Empty)
        {
            errors["foodId"] = ["Укажите продукт из справочника."];
        }

        var quantity = request?.Quantity ?? 0;

        if (quantity < MinQuantity || quantity > MaxQuantity)
        {
            errors["quantity"] = [$"Количество должно быть в диапазоне {MinQuantity}-{MaxQuantity}."];
        }

        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var date = request?.Date ?? today;

        if (date > today)
        {
            errors["date"] = ["Дата не может быть в будущем."];
        }

        // Продукт нужен и для проверки единицы, и для ответа, поэтому грузим его
        // сразу; ошибка «не найден» не мешает собрать остальные ошибки.
        Food? food = null;

        if (request?.FoodId is Guid foodId && foodId != Guid.Empty)
        {
            food = await db.Foods
                .Include(candidate => candidate.Names)
                .FirstOrDefaultAsync(candidate => candidate.Id == foodId, cancellationToken);

            if (food is null)
            {
                errors["foodId"] = ["Продукт не найден. Выберите продукт из поиска."];
            }
        }

        // Единица записи обязана совпадать с базовой единицей продукта: КБЖУ
        // задано на 100 г или на 100 мл, и без плотности перевести одно в другое
        // нельзя. Молча подставлять граммы для молока - значит врать в калориях.
        var unit = requestedUnit ?? food?.BaseUnit ?? FoodUnit.Gram;

        if (food is not null && requestedUnit is not null && requestedUnit != food.BaseUnit)
        {
            errors["unit"] = [$"У продукта базовая единица {food.BaseUnit}; укажите {food.BaseUnit}."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var now = clock.GetUtcNow();
        var meal = await GetOrCreateMealAsync(db, userId.Value, date, kind!.Value, now, cancellationToken);

        var item = new MealItem
        {
            Id = Guid.NewGuid(),
            MealId = meal.Id,
            FoodId = food!.Id,
            Quantity = quantity,
            Unit = unit,
            CreatedAt = now,
        };

        db.MealItems.Add(item);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Created(
            $"/api/v1/diary/{item.Id}",
            DiaryMapping.ToEntry(item.Id, food, item.Quantity, item.Unit));
    }

    /// <summary>
    /// Прием пищи на дату: существующий либо только что созданный. Уникальный
    /// индекс (UserId, Date, Kind) защищает от двух одновременных запросов; на
    /// конфликте забираем чужую строку и пишем позицию в нее.
    /// </summary>
    private static async Task<Meal> GetOrCreateMealAsync(
        NormaDbContext db,
        Guid userId,
        DateOnly date,
        MealKind kind,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var existing = await db.Meals
            .FirstOrDefaultAsync(
                meal => meal.UserId == userId && meal.Date == date && meal.Kind == kind,
                cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var created = new Meal
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Date = date,
            Kind = kind,
            CreatedAt = now,
        };

        db.Meals.Add(created);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return created;
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            db.Entry(created).State = EntityState.Detached;

            return await db.Meals.FirstAsync(
                meal => meal.UserId == userId && meal.Date == date && meal.Kind == kind,
                cancellationToken);
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static MealKind? ParseMealKind(string? raw, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            errors["meal"] = [$"Укажите прием пищи: {string.Join(", ", Enum.GetNames<MealKind>())}."];
            return null;
        }

        if (!Enum.TryParse<MealKind>(raw, ignoreCase: true, out var kind) || !Enum.IsDefined(kind))
        {
            errors["meal"] = [$"Недопустимый прием пищи: ожидается {string.Join(", ", Enum.GetNames<MealKind>())}."];
            return null;
        }

        return kind;
    }

    private static FoodUnit? ParseUnit(string? raw, Dictionary<string, string[]> errors)
    {
        // Единица необязательна: по умолчанию берется базовая у продукта.
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (!Enum.TryParse<FoodUnit>(raw, ignoreCase: true, out var unit) || !Enum.IsDefined(unit))
        {
            errors["unit"] = [$"Недопустимая единица: ожидается {string.Join(", ", Enum.GetNames<FoodUnit>())}."];
            return null;
        }

        return unit;
    }
}
