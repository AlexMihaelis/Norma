using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Norma.Api.Data;
using Norma.Api.Features.Auth;

namespace Norma.Api.Features.Water.DeleteLastWater;

/// <summary>
/// Срез «отменить последний прием воды»: шаг назад после случайного плюса.
/// Удаляется самый свежий прием за день, ответ - день после удаления.
/// </summary>
public static class DeleteLastWaterEndpoint
{
    public static IEndpointRouteBuilder MapDeleteLastWater(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapDelete("/water/last", Handle)
            .WithName("DeleteLastWater")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<Results<Ok<WaterDayResponse>, NotFound, UnauthorizedHttpResult>> Handle(
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

        var last = await db.WaterIntakes
            .Where(intake => intake.UserId == userId && intake.Date == day)
            .OrderByDescending(intake => intake.CreatedAt)
            .ThenByDescending(intake => intake.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (last is null)
        {
            return TypedResults.NotFound();
        }

        db.WaterIntakes.Remove(last);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(await WaterDayReader.ReadAsync(db, userId.Value, day, cancellationToken));
    }
}
