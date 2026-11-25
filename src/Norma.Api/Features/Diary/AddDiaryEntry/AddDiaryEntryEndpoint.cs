using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Norma.Api.Data;
using Norma.Api.Data.Entities;
using Norma.Api.Features.Auth;

namespace Norma.Api.Features.Diary.AddDiaryEntry;

/// <summary>
/// Срез «записать съеденное»: находит или создает прием пищи на дату и кладет в
/// него позицию. КБЖУ не сохраняется - оно считается из продукта при чтении.
/// </summary>
/// <remarks>
/// Прием пищи создается лениво: строка <see cref="Meal"/> появляется вместе с
/// первой позицией.
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

        var kind = DiaryParsing.ParseRequiredMeal(request?.Meal, errors);
        var requestedUnit = DiaryParsing.ParseUnit(request?.Unit, errors);

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

        // Допуск в сутки: у пользователя может быть уже следующий день по местному времени.
        if (date > today.AddDays(1))
        {
            errors["date"] = ["Дата не может быть в будущем."];
        }

        // Продукт нужен и для проверки единицы, и для ответа.
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

        // Единица записи обязана совпадать с базовой: без плотности граммы и
        // миллилитры не перевести, а подставить одно вместо другого - значит врать
        // в калориях.
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
        var meal = await DiaryMeals.GetOrCreateAsync(
            db, userId.Value, date, kind!.Value, now, cancellationToken);

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
}
