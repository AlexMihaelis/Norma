using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Norma.Api.Data;
using Norma.Api.Features.Auth;

namespace Norma.Api.Features.Water.GetWaterDay;

/// <summary>
/// Срез «вода за день»: сумма приемов и ориентир по профилю. Не хватает пола
/// или веса - норма не выдумывается, ответ подсказывает, что заполнить.
/// </summary>
public static class GetWaterDayEndpoint
{
    public static IEndpointRouteBuilder MapGetWaterDay(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet("/water", Handle)
            .WithName("GetWaterDay")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<Results<Ok<WaterDayResponse>, UnauthorizedHttpResult>> Handle(
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

        return TypedResults.Ok(await WaterDayReader.ReadAsync(db, userId.Value, day, cancellationToken));
    }
}
