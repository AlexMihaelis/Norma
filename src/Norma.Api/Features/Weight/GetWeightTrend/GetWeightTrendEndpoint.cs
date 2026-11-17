using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Norma.Api.Data;
using Norma.Api.Features.Auth;
using Norma.Core.Trend;

namespace Norma.Api.Features.Weight.GetWeightTrend;

/// <summary>
/// Срез «тренд веса»: сглаживание EMA, скорость и прогноз даты цели. Цель
/// берется из профиля (желаемый вес), арифметика живёт в <see cref="TrendMath"/>.
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

    private static async Task<Results<Ok<WeightTrendResponse>, UnauthorizedHttpResult>> Handle(
        ClaimsPrincipal principal,
        NormaDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();

        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        // Цель по весу - из профиля: у среза один источник правды, как у плана.
        var targetKg = await db.UserProfiles
            .AsNoTracking()
            .Where(profile => profile.UserId == userId)
            .Select(profile => profile.TargetWeightKg)
            .FirstOrDefaultAsync(cancellationToken);

        // Выбираем только свои замеры: userId берётся из токена, а не из запроса.
        var rows = await db.WeightMeasurements
            .Where(measurement => measurement.UserId == userId)
            .OrderBy(measurement => measurement.Date)
            .Select(measurement => new { measurement.Date, measurement.Kg })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return TypedResults.Ok(new WeightTrendResponse(null, null, targetKg, null, []));
        }

        var entries = rows.Select(row => new WeightEntry(row.Date, row.Kg)).ToList();
        var trend = TrendMath.Analyze(entries, targetKg);

        var historyDays = entries[^1].Date.DayNumber - entries[0].Date.DayNumber + 1;
        var hasEnoughHistory = historyDays >= MinHistoryDays;

        var points = trend.Points
            .Select(point => new TrendPointResponse(point.Date, point.EmaKg))
            .ToList();

        return TypedResults.Ok(new WeightTrendResponse(
            trend.CurrentEmaKg,
            hasEnoughHistory ? trend.RateKgPerWeek : null,
            targetKg,
            hasEnoughHistory ? trend.GoalDate : null,
            points));
    }
}
