using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Norma.Api.Data;
using Norma.Api.Features.Auth;
using Norma.Core.Energy;
using Norma.Core.Stats;

namespace Norma.Api.Features.Stats.GetStats;

/// <summary>
/// Срез «статистика за период»: съеденное по дням, средние по дням с записями,
/// расход активности и отклонение от цели.
/// </summary>
/// <remarks>
/// Цель считается на каждую дату по весу того дня: прошлый месяц не должен
/// меняться из-за сегодняшнего замера. Профиль (пол, рост, активность, цель)
/// хранится без истории, поэтому берётся текущий - это ограничение отражено в
/// ответе через <see cref="StatsText.TargetNote"/>.
/// </remarks>
public static class GetStatsEndpoint
{
    private const int DefaultPeriodDays = 7;
    private const int MaxPeriodDays = 366;
    private const double DefaultKgPerWeek = 0.25;

    public static IEndpointRouteBuilder MapGetStats(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet("/stats", Handle)
            .WithName("GetStats")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<Results<Ok<StatsResponse>, ValidationProblem, UnauthorizedHttpResult>> Handle(
        DateOnly? from,
        DateOnly? to,
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
        var end = to ?? today;
        var start = from ?? end.AddDays(-(DefaultPeriodDays - 1));

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (start > end)
        {
            errors["from"] = ["Начало периода не может быть позже конца."];
        }

        if (end > today)
        {
            errors["to"] = ["Конец периода не может быть в будущем."];
        }

        if (end.DayNumber - start.DayNumber + 1 > MaxPeriodDays)
        {
            errors["from"] = [$"Период не может быть длиннее {MaxPeriodDays} дней."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        // Съеденное по дням. КБЖУ позиции = количество × значение продукта / 100,
        // поэтому агрегат считается прямо в базе, без выгрузки всех позиций.
        var intakeByDay = await db.MealItems
            .Where(item => item.Meal!.UserId == userId && item.Meal.Date >= start && item.Meal.Date <= end)
            .GroupBy(item => item.Meal!.Date)
            .Select(group => new
            {
                Date = group.Key,
                Kcal = group.Sum(item => item.Quantity * item.Food!.KcalPer100g) / 100.0,
                Protein = group.Sum(item => item.Quantity * item.Food!.ProteinPer100g) / 100.0,
                Fat = group.Sum(item => item.Quantity * item.Food!.FatPer100g) / 100.0,
                Carbs = group.Sum(item => item.Quantity * item.Food!.CarbsPer100g) / 100.0,
                Entries = group.Count(),
            })
            .ToListAsync(cancellationToken);

        var activityByDay = await db.ActivityRecords
            .Where(record => record.UserId == userId && record.Date >= start && record.Date <= end)
            .GroupBy(record => record.Date)
            .Select(group => new { Date = group.Key, Kcal = group.Sum(record => record.Kcal) })
            .ToListAsync(cancellationToken);

        // Вес на каждую дату: последний замер не позже этой даты, без заглядывания
        // в будущее. Берём всю историю до конца периода одним запросом.
        var weights = await db.WeightMeasurements
            .Where(measurement => measurement.UserId == userId && measurement.Date <= end)
            .OrderBy(measurement => measurement.Date)
            .ThenBy(measurement => measurement.CreatedAt)
            .Select(measurement => new WeightPoint(measurement.Date, measurement.Kg))
            .ToListAsync(cancellationToken);

        var profile = await db.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken);

        var profileReady = profile is not null
            && profile.Sex is not null
            && profile.BirthDate is not null
            && profile.HeightCm is not null
            && profile.Activity is not null
            && profile.Goal is not null;

        var activityKcalByDay = activityByDay.ToDictionary(day => day.Date, day => day.Kcal);
        var days = new List<StatsDayResponse>();
        var dayIntakes = new List<DayIntake>();

        foreach (var day in intakeByDay.OrderBy(day => day.Date))
        {
            var weightKg = WeightOnOrBefore(weights, day.Date);

            var targetKcal = profileReady && weightKg is not null
                ? EnergyMath.Plan(
                        ProfileBuilder.From(
                            profile!.Sex!.Value,
                            profile.BirthDate!.Value,
                            day.Date,
                            profile.HeightCm!.Value,
                            weightKg.Value,
                            profile.Activity!.Value),
                        profile.Goal!.Value,
                        profile.KgPerWeek ?? DefaultKgPerWeek)
                    .TargetKcal
                : (int?)null;

            var kcal = (int)Math.Round(day.Kcal, MidpointRounding.AwayFromZero);

            days.Add(new StatsDayResponse(
                day.Date,
                kcal,
                Math.Round(day.Protein, 1, MidpointRounding.AwayFromZero),
                Math.Round(day.Fat, 1, MidpointRounding.AwayFromZero),
                Math.Round(day.Carbs, 1, MidpointRounding.AwayFromZero),
                day.Entries,
                activityKcalByDay.GetValueOrDefault(day.Date),
                targetKcal,
                targetKcal is null ? null : kcal - targetKcal));

            dayIntakes.Add(new DayIntake(
                day.Date, day.Kcal, day.Protein, day.Fat, day.Carbs, day.Entries));
        }

        var averages = StatsMath.Average(dayIntakes);

        var targets = days
            .Where(day => day.TargetKcal is not null)
            .Select(day => day.TargetKcal!.Value)
            .ToList();

        var deviation = targets.Count > 0
            ? StatsMath.Deviation(averages.Kcal, targets.Average())
            : null;

        return TypedResults.Ok(new StatsResponse(
            start,
            end,
            days,
            new StatsAveragesResponse(
                days.Count,
                averages.Kcal,
                averages.Protein,
                averages.Fat,
                averages.Carbs,
                days.Sum(day => day.ActivityKcal),
                targets.Count > 0
                    ? (int)Math.Round(targets.Average(), MidpointRounding.AwayFromZero)
                    : null),
            deviation is null
                ? null
                : new StatsDeviationResponse(
                    deviation.TargetKcal, deviation.DiffKcal, deviation.DiffPercent),
            BuildHint(profileReady, days.Count, targets.Count),
            StatsText.TargetNote));
    }

    /// <summary>Последний вес не позже даты; <c>null</c> - замеров до этой даты нет.</summary>
    private static double? WeightOnOrBefore(IReadOnlyList<WeightPoint> weights, DateOnly day)
    {
        double? result = null;

        foreach (var weight in weights)
        {
            if (weight.Date <= day)
            {
                result = weight.Kg;
            }
            else
            {
                break;
            }
        }

        return result;
    }

    /// <summary>Замер веса, уже спроецированный из базы.</summary>
    private sealed record WeightPoint(DateOnly Date, double Kg);

    private static string? BuildHint(bool profileReady, int daysWithEntries, int daysWithTarget)
    {
        // Пустой период не нуждается в подсказках: пользователю просто нечего смотреть.
        if (daysWithEntries == 0)
        {
            return null;
        }

        if (!profileReady)
        {
            return "Заполните профиль (пол, дата рождения, рост, активность, цель) - без него цель не рассчитана.";
        }

        if (daysWithTarget == 0)
        {
            return "Нет замеров веса на эти даты - цель не рассчитана.";
        }

        if (daysWithTarget < daysWithEntries)
        {
            return "Для части дней нет замера веса на дату - цель рассчитана не для всех дней.";
        }

        return null;
    }
}
