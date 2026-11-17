using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Norma.Api.Data;
using Norma.Api.Features.Auth;

namespace Norma.Api.Features.Activity.GetActivityDay;

/// <summary>
/// Срез «активность за день»: записи пользователя и суммарный расход. Калории
/// берутся из снимка, поэтому сумма не зависит от более поздних замеров веса.
/// </summary>
public static class GetActivityDayEndpoint
{
    public static IEndpointRouteBuilder MapGetActivityDay(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet("/activity", Handle)
            .WithName("GetActivityDay")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<Results<Ok<ActivityDayResponse>, UnauthorizedHttpResult>> Handle(
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

        var items = await db.ActivityRecords
            .Where(record => record.UserId == userId && record.Date == day)
            .OrderBy(record => record.CreatedAt)
            .Select(record => new ActivityResponse(
                record.Id,
                record.Date,
                record.Kind,
                record.Minutes,
                record.Kcal))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new ActivityDayResponse(
            day,
            items,
            items.Sum(item => item.Kcal)));
    }
}
