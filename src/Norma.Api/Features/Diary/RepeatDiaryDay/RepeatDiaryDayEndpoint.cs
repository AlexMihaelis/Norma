using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Norma.Api.Data;
using Norma.Api.Data.Entities;
using Norma.Api.Features.Auth;

namespace Norma.Api.Features.Diary.RepeatDiaryDay;

/// <summary>
/// Срез «повторить день»: копирует позиции одного дня в другой. По умолчанию -
/// вчерашний день в сегодняшний, потому что это самый частый сценарий.
/// </summary>
/// <remarks>
/// Копируются позиции, а не прием: копии добавляются в существующий прием дня,
/// поэтому второго завтрака не появляется.
/// </remarks>
public static class RepeatDiaryDayEndpoint
{
    public static IEndpointRouteBuilder MapRepeatDiaryDay(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost("/diary/repeat", Handle)
            .WithName("RepeatDiaryDay")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<Results<Ok<RepeatDiaryResponse>, ValidationProblem, UnauthorizedHttpResult>> Handle(
        RepeatDiaryRequest? request,
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

        var onlyKind = DiaryParsing.ParseOptionalMeal(request?.Meal, errors);

        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var targetDate = request?.Date ?? today;
        var sourceDate = request?.FromDate ?? targetDate.AddDays(-1);

        // Допуск в сутки: клиент живет в местном времени, сервер считает по UTC.
        if (targetDate > today.AddDays(1))
        {
            errors["date"] = ["Дата не может быть в будущем."];
        }

        if (sourceDate > today.AddDays(1))
        {
            errors["fromDate"] = ["Дата источника не может быть в будущем."];
        }

        if (sourceDate == targetDate)
        {
            errors["fromDate"] = ["Источник и цель совпадают: выберите другой день."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        // Без Include продукта: для копирования нужны только ссылка, количество и
        // единица.
        var sourceItems = await db.MealItems
            .Where(item => item.Meal!.UserId == userId && item.Meal.Date == sourceDate
                && (onlyKind == null || item.Meal.Kind == onlyKind))
            .Include(item => item.Meal)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        if (sourceItems.Count == 0)
        {
            var emptyDay = await DiaryDayReader.ReadAsync(db, userId.Value, targetDate, cancellationToken);

            return TypedResults.Ok(new RepeatDiaryResponse(
                emptyDay.Date,
                0,
                "В выбранном дне нет позиций - копировать нечего.",
                emptyDay.Meals,
                emptyDay.Totals));
        }

        var now = clock.GetUtcNow();
        var kinds = sourceItems.Select(item => item.Meal!.Kind).Distinct().ToList();

        var meals = await DiaryMeals.GetOrCreateAsync(
            db, userId.Value, targetDate, kinds, now, cancellationToken);

        // День сортируется по CreatedAt, поэтому копии получают нарастающие метки:
        // так порядок позиций повторит исходный.
        var tick = 0;

        foreach (var source in sourceItems)
        {
            db.MealItems.Add(new MealItem
            {
                Id = Guid.NewGuid(),
                MealId = meals[source.Meal!.Kind].Id,
                FoodId = source.FoodId,
                Quantity = source.Quantity,
                Unit = source.Unit,
                CreatedAt = now.AddTicks(tick++),
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        var day = await DiaryDayReader.ReadAsync(db, userId.Value, targetDate, cancellationToken);

        return TypedResults.Ok(new RepeatDiaryResponse(
            day.Date,
            sourceItems.Count,
            null,
            day.Meals,
            day.Totals));
    }
}
