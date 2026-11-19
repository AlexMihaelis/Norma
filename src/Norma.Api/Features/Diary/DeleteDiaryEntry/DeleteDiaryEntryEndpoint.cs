using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Norma.Api.Data;
using Norma.Api.Features.Auth;

namespace Norma.Api.Features.Diary.DeleteDiaryEntry;

/// <summary>
/// Срез «удалить позицию дневника»: истории правок нет, ошибочную запись убирают и
/// добавляют заново.
/// </summary>
public static class DeleteDiaryEntryEndpoint
{
    public static IEndpointRouteBuilder MapDeleteDiaryEntry(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapDelete("/diary/{id:guid}", Handle)
            .WithName("DeleteDiaryEntry")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<Results<NoContent, NotFound, UnauthorizedHttpResult>> Handle(
        Guid id,
        ClaimsPrincipal principal,
        NormaDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();

        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        // Чужую позицию нельзя удалить даже по идентификатору: фильтр по владельцу
        // стоит в самом запросе.
        var item = await db.MealItems
            .Include(entry => entry.Meal)
            .FirstOrDefaultAsync(
                entry => entry.Id == id && entry.Meal!.UserId == userId,
                cancellationToken);

        if (item is null)
        {
            return TypedResults.NotFound();
        }

        var mealHasOtherItems = await db.MealItems
            .AnyAsync(
                other => other.MealId == item.MealId && other.Id != id,
                cancellationToken);

        db.MealItems.Remove(item);

        // Пустой прием занял бы уникальный индекс (UserId, Date, Kind), поэтому
        // уходит вместе с последней позицией.
        if (!mealHasOtherItems && item.Meal is not null)
        {
            db.Meals.Remove(item.Meal);
        }

        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }
}
