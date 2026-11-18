using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Norma.Api.Data;
using Norma.Api.Data.Entities;
using Norma.Api.Features.Auth;

namespace Norma.Api.Features.Diary.GetDiaryDay;

/// <summary>
/// Срез «дневник за день»: шесть приемов пищи в хронологическом порядке, позиции
/// с посчитанным КБЖУ, итоги по приему и по дню.
/// </summary>
public static class GetDiaryDayEndpoint
{
    public static IEndpointRouteBuilder MapGetDiaryDay(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet("/diary", Handle)
            .WithName("GetDiaryDay")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<Results<Ok<DiaryDayResponse>, UnauthorizedHttpResult>> Handle(
        DateOnly? date,
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

        var day = date ?? DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

        // Один запрос на день: позиции вместе с продуктом и названием. КБЖУ
        // считается в памяти: так округление позиции совпадает с тем, что видит
        // пользователь, а не с суммой до округления.
        var items = await db.MealItems
            .Where(item => item.Meal!.UserId == userId && item.Meal.Date == day)
            .Include(item => item.Meal)
            .Include(item => item.Food)
            .ThenInclude(food => food!.Names)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        // Все шесть приемов отдаются всегда, даже пустые: UI рисует день целиком
        // и не должен угадывать, какие блоки существуют.
        var meals = Enum.GetValues<MealKind>()
            .Select(kind =>
            {
                var mealItems = items
                    .Where(item => item.Meal!.Kind == kind)
                    .Select(DiaryMapping.ToEntry)
                    .ToList();

                return new DiaryMealResponse(kind, mealItems, DiaryMapping.Totals(mealItems));
            })
            .ToList();

        return TypedResults.Ok(new DiaryDayResponse(
            day,
            meals,
            DiaryMapping.Totals(meals.SelectMany(meal => meal.Items))));
    }
}
