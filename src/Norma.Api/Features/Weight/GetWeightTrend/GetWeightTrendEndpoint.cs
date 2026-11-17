using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Norma.Api.Data;
using Norma.Api.Features.Auth;
using Norma.Core.Age;
using Norma.Core.Bmi;
using Norma.Core.Trend;

namespace Norma.Api.Features.Weight.GetWeightTrend;

/// <summary>
/// Срез «тренд веса»: сглаживание EMA, скорость, прогноз даты цели, ИМТ и
/// норма веса. Норма зависит от роста и возраста, цель берется из профиля
/// (желаемый вес); арифметика живёт в <see cref="TrendMath"/>,
/// <see cref="BmiMath"/> и <see cref="NormalWeightMath"/>.
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
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();

        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

        // Цель, рост, пол и дата рождения - из профиля.
        var profile = await db.UserProfiles
            .AsNoTracking()
            .Where(candidate => candidate.UserId == userId)
            .Select(candidate => new
            {
                candidate.TargetWeightKg,
                candidate.HeightCm,
                candidate.Sex,
                candidate.BirthDate,
            })
            .FirstOrDefaultAsync(cancellationToken);

        var targetKg = profile?.TargetWeightKg;
        var heightCm = profile?.HeightCm;
        var sex = profile?.Sex;
        var age = profile?.BirthDate is DateOnly birthDate
            ? AgeMath.Years(birthDate, today)
            : (int?)null;
        var ageBand = NormalWeightMath.BandFor(age);

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

        NormalWeightRange? normal = null;
        double? bmi = null;
        BmiCategory? bmiCategory = null;
        double? obeseFromKg = null;

        // Границы нормы зависят от роста и возраста, а не от замеров: их можно
        // показать даже без истории. Детям норма по перцентилям не считается.
        if (heightCm is double height)
        {
            normal = NormalWeightMath.For(age, height);

            if (normal is not null)
            {
                obeseFromKg = BmiMath.ObeseFromKg(height);
            }

            if (latest is not null)
            {
                bmi = BmiMath.Value(latest.Kg, height);

                if (normal is not null)
                {
                    bmiCategory = NormalWeightMath.CategoryFor(bmi.Value, normal);
                }
            }
        }

        double? currentEmaKg = null;
        double? rateKgPerWeek = null;
        DateOnly? goalDate = null;
        IReadOnlyList<TrendPointResponse> points = [];

        if (rows.Count > 0)
        {
            var entries = rows
                .OrderBy(row => row.Date)
                .Select(row => new WeightEntry(row.Date, row.Kg))
                .ToList();

            var trend = TrendMath.Analyze(entries, targetKg);

            var historyDays = entries[^1].Date.DayNumber - entries[0].Date.DayNumber + 1;
            var hasEnoughHistory = historyDays >= MinHistoryDays;

            currentEmaKg = trend.CurrentEmaKg;
            rateKgPerWeek = hasEnoughHistory ? trend.RateKgPerWeek : null;
            goalDate = hasEnoughHistory ? trend.GoalDate : null;
            points = trend.Points
                .Select(point => new TrendPointResponse(point.Date, point.EmaKg))
                .ToList();
        }

        return TypedResults.Ok(new WeightTrendResponse(
            currentEmaKg,
            rateKgPerWeek,
            targetKg,
            goalDate,
            heightCm,
            sex,
            age,
            ageBand,
            bmi,
            bmiCategory,
            obeseFromKg,
            normal is null
                ? null
                : new NormalWeightResponse(normal.MinKg, normal.MaxKg, normal.BmiMin, normal.BmiMax),
            points));
    }
}
