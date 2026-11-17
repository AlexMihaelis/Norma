using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Norma.Api.Data;
using Norma.Api.Features.Auth;
using Norma.Core.Bmi;
using Norma.Core.Trend;

namespace Norma.Api.Features.Weight.GetWeightTrend;

/// <summary>
/// Срез «тренд веса»: сглаживание EMA, скорость, прогноз даты цели, ИМТ и
/// границы весовых зон. Цель берется из профиля (желаемый вес), арифметика
/// живёт в <see cref="TrendMath"/> и <see cref="BmiMath"/>.
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

        // Цель по весу и рост - из профиля: у среза один источник правды.
        var profile = await db.UserProfiles
            .AsNoTracking()
            .Where(candidate => candidate.UserId == userId)
            .Select(candidate => new { candidate.TargetWeightKg, candidate.HeightCm })
            .FirstOrDefaultAsync(cancellationToken);

        var targetKg = profile?.TargetWeightKg;
        var heightCm = profile?.HeightCm;

        // Выбираем только свои замеры: userId берётся из токена, а не из запроса.
        var rows = await db.WeightMeasurements
            .Where(measurement => measurement.UserId == userId)
            .Select(measurement => new { measurement.Date, measurement.Kg, measurement.CreatedAt })
            .ToListAsync(cancellationToken);

        // Последний замер по дате, при равенстве дат - по времени записи.
        var latest = rows
            .OrderBy(row => row.Date)
            .ThenBy(row => row.CreatedAt)
            .LastOrDefault();

        BmiResult? bmi = null;
        double? healthyMinKg = null;
        double? healthyMaxKg = null;
        double? obeseFromKg = null;

        // Границы зон зависят только от роста: их можно показать даже без замеров.
        if (heightCm is double height)
        {
            healthyMinKg = BmiMath.HealthyWeightMinKg(height);
            healthyMaxKg = BmiMath.HealthyWeightMaxKg(height);
            obeseFromKg = BmiMath.ObeseFromKg(height);

            if (latest is not null)
            {
                bmi = BmiMath.ForMeasurement(latest.Kg, height);
            }
        }

        if (rows.Count == 0)
        {
            return TypedResults.Ok(new WeightTrendResponse(
                CurrentEmaKg: null,
                RateKgPerWeek: null,
                TargetKg: targetKg,
                GoalDate: null,
                HeightCm: heightCm,
                Bmi: null,
                BmiCategory: null,
                HealthyWeightMinKg: healthyMinKg,
                HealthyWeightMaxKg: healthyMaxKg,
                ObeseFromKg: obeseFromKg,
                Points: []));
        }

        var entries = rows
            .OrderBy(row => row.Date)
            .Select(row => new WeightEntry(row.Date, row.Kg))
            .ToList();

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
            heightCm,
            bmi?.Value,
            bmi?.Category,
            healthyMinKg,
            healthyMaxKg,
            obeseFromKg,
            points));
    }
}
