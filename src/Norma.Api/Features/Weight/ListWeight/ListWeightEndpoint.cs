using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Norma.Api.Data;
using Norma.Api.Features.Auth;

namespace Norma.Api.Features.Weight.ListWeight;

/// <summary>Срез «мои замеры веса»: только замеры текущего пользователя.</summary>
public static class ListWeightEndpoint
{
    private const int MaxItems = 100;

    public static IEndpointRouteBuilder MapListWeight(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet("/weight", Handle)
            .WithName("ListWeight")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<Results<Ok<WeightListResponse>, UnauthorizedHttpResult>> Handle(
        ClaimsPrincipal principal,
        NormaDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();

        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        var items = await db.WeightMeasurements
            .Where(measurement => measurement.UserId == userId)
            .OrderByDescending(measurement => measurement.Date)
            .ThenByDescending(measurement => measurement.CreatedAt)
            .Take(MaxItems)
            .Select(measurement => new WeightResponse(measurement.Id, measurement.Date, measurement.Kg))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new WeightListResponse(items));
    }
}
