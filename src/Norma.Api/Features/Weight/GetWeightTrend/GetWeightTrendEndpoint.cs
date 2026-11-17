using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Norma.Api.Data;
using Norma.Api.Features.Auth;
using Norma.Core.Trend;

namespace Norma.Api.Features.Weight.GetWeightTrend;

/// <summary>
/// Срез «тренд веса»: сглаживание EMA, скорость и прогноз даты цели.
/// Арифметика живёт в <see cref="TrendMath"/>, срез только достаёт замеры.
/// </summary>
public static class GetWeightTrendEndpoint
{
    // Скорость по робастной линии требует истории хотя бы в четыре недели:
    // раньше не хватает данных отделить тренд от воды и праздников.
    private const int MinHistoryDays = 28;

    public static IEndpointRouteBuilder MapGetWeightTrend(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet("/weight/trend", Handle)
            .WithName("GetWeightTrend")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<Results<Ok<WeightTrendResponse>, ValidationProblem, UnauthorizedHttpResult>> Handle(
        double? goalKg,
        ClaimsPrincipal principal,
        NormaDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();

        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (goalKg is <= 0 or > 500)
        {
            errors["goalKg"] = ["Цель по весу должна быть в диапазоне 0-500 кг."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        // Выбираем только свои замеры: userId берётся из токена, а не из запроса.
        var rows = await db.WeightMeasurements
            .Where(measurement => measurement.UserId == userId)
            .OrderBy(measurement => measurement.Date)
            .Select(measurement => new { measurement.Date, measurement.Kg })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return TypedResults.Ok(new WeightTrendResponse(null, null, null, []));
        }

        var entries = rows.Select(row => new WeightEntry(row.Date, row.Kg)).ToList();
        var trend = TrendMath.Analyze(entries, goalKg);

        var historyDays = entries[^1].Date.DayNumber - entries[0].Date.DayNumber + 1;
        var hasEnoughHistory = historyDays >= MinHistoryDays;

        var points = trend.Points
            .Select(point => new TrendPointResponse(point.Date, point.EmaKg))
            .ToList();

        return TypedResults.Ok(new WeightTrendResponse(
            trend.CurrentEmaKg,
            hasEnoughHistory ? trend.RateKgPerWeek : null,
            hasEnoughHistory ? trend.GoalDate : null,
            points));
    }
}
