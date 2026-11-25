using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Norma.Api.Data;
using Norma.Api.Data.Entities;
using Norma.Api.Features.Auth;

namespace Norma.Api.Features.BodyMeasurements.GetLatestBodyMeasurements;

/// <summary>
/// Последние обхваты по каждому виду: значение, дата и изменение к предыдущему
/// замеру. Так карточка «Замеры тела» рисуется одним запросом.
/// </summary>
public static class GetLatestBodyMeasurementsEndpoint
{
    private const int MaxRows = 500;

    public static IEndpointRouteBuilder MapGetLatestBodyMeasurements(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet("/body-measurements/latest", Handle)
            .WithName("GetLatestBodyMeasurements")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<Results<Ok<IReadOnlyList<BodyMeasurementLatestResponse>>, UnauthorizedHttpResult>> Handle(
        ClaimsPrincipal principal,
        NormaDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();

        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        var rows = await db.BodyMeasurements
            .Where(measurement => measurement.UserId == userId)
            .OrderByDescending(measurement => measurement.Date)
            .ThenByDescending(measurement => measurement.CreatedAt)
            .Take(MaxRows)
            .ToListAsync(cancellationToken);

        var result = new List<BodyMeasurementLatestResponse>();

        foreach (var kind in Enum.GetValues<BodyMeasurementKind>())
        {
            var measurements = rows.Where(row => row.Kind == kind).ToList();

            if (measurements.Count == 0)
            {
                continue;
            }

            var latest = measurements[0];
            var previous = measurements.Count > 1 ? measurements[1] : null;

            result.Add(new BodyMeasurementLatestResponse(
                kind,
                latest.Date,
                latest.Cm,
                previous?.Date,
                previous?.Cm,
                previous is null ? null : Math.Round(latest.Cm - previous.Cm, 1)));
        }

        return TypedResults.Ok<IReadOnlyList<BodyMeasurementLatestResponse>>(result);
    }
}