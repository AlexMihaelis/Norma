using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Norma.Api.Data;
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

        return TypedResults.Ok(
            await DiaryDayReader.ReadAsync(db, userId.Value, day, cancellationToken));
    }
}
