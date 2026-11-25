using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Norma.Api.Data;
using Norma.Api.Data.Entities;
using Norma.Api.Features.Auth;
using Norma.Core.Energy;
using Norma.Core.Stats;

namespace Norma.Api.Features.Stats.GetStats;

/// <summary>
/// Срез «статистика за период»: съеденное по дням, средние по дням с записями,
/// расход активности и отклонение от цели.
/// </summary>
/// <remarks>
/// Цель считается на каждую дату по весу дня и снимку профиля, действовавшему на
/// этот день: прошлый месяц не должен меняться из-за сегодняшнего замера или
/// смены цели.
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

        // Допуск в сутки: клиент присылает местную дату, а сервер считает сегодня по UTC.
        if (end > today.AddDays(1))
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

        // Агрегат считается в базе: КБЖУ позиции = количество × значение / 100.
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

        // Вес на дату - последний замер не позже этой даты.
        var weights = await db.WeightMeasurements
            .Where(measurement => measurement.UserId == userId && measurement.Date <= end)
            .OrderBy(measurement => measurement.Date)
            .ThenBy(measurement => measurement.CreatedAt)
            .Select(measurement => new WeightPoint(measurement.Date, measurement.Kg))
            .ToListAsync(cancellationToken);

        // Профиль на дату: последний снимок не позже дня. Снимки пишутся при
        // сохранении профиля, поэтому цель прошлых дней не меняется задним числом.
        var snapshots = await db.UserProfileSnapshots
            .AsNoTracking()
            .Where(snapshot => snapshot.UserId == userId)
            .OrderBy(snapshot => snapshot.EffectiveFrom)
            .ThenBy(snapshot => snapshot.CreatedAt)
            .ToListAsync(cancellationToken);

        var profile = await db.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken);

        // Запасной вариант для дней до первого снимка: текущий профиль.
        var currentProfile = profile is null ? null : ProfileState.From(profile);

        var activityKcalByDay = activityByDay.ToDictionary(day => day.Date, day => day.Kcal);
        var days = new List<StatsDayResponse>();
        var dayIntakes = new List<DayIntake>();

        foreach (var day in intakeByDay.OrderBy(day => day.Date))
        {
            var weightKg = WeightOnOrBefore(weights, day.Date);
            var state = ProfileOn(day.Date, snapshots) ?? currentProfile;

            var targetKcal = state is not null && state.Ready && weightKg is not null
                ? EnergyMath.Plan(
                        ProfileBuilder.From(
                            state.Sex!.Value,
                            state.BirthDate!.Value,
                            day.Date,
                            state.HeightCm!.Value,
                            weightKg.Value,
                            state.Activity!.Value),
                        state.Goal!.Value,
                        state.KgPerWeek ?? DefaultKgPerWeek)
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
                weightKg,
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

        var anyProfileReady = currentProfile?.Ready == true
            || snapshots.Any(snapshot => ProfileState.From(snapshot).Ready);

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
            BuildHint(anyProfileReady, days.Count, targets.Count),
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

    /// <summary>Снимок профиля, действовавший на дату; <c>null</c> - снимков еще нет.</summary>
    private static ProfileState? ProfileOn(DateOnly day, IReadOnlyList<UserProfileSnapshot> snapshots)
    {
        UserProfileSnapshot? found = null;

        foreach (var snapshot in snapshots)
        {
            if (snapshot.EffectiveFrom <= day)
            {
                found = snapshot;
            }
            else
            {
                break;
            }
        }

        return found is null ? null : ProfileState.From(found);
    }

    /// <summary>Замер веса, уже спроецированный из базы.</summary>
    private sealed record WeightPoint(DateOnly Date, double Kg);

    /// <summary>Поля профиля, нужные для плана; одинаково читаются из профиля и снимка.</summary>
    private sealed record ProfileState(
        Sex? Sex,
        DateOnly? BirthDate,
        double? HeightCm,
        ActivityLevel? Activity,
        Goal? Goal,
        double? KgPerWeek)
    {
        public bool Ready =>
            Sex is not null
            && BirthDate is not null
            && HeightCm is not null
            && Activity is not null
            && Goal is not null;

        public static ProfileState From(UserProfile profile) =>
            new(profile.Sex, profile.BirthDate, profile.HeightCm, profile.Activity, profile.Goal, profile.KgPerWeek);

        public static ProfileState From(UserProfileSnapshot snapshot) =>
            new(snapshot.Sex, snapshot.BirthDate, snapshot.HeightCm, snapshot.Activity, snapshot.Goal, snapshot.KgPerWeek);
    }

    private static string? BuildHint(bool anyProfileReady, int daysWithEntries, int daysWithTarget)
    {
        // Пустой период не нуждается в подсказках: пользователю просто нечего смотреть.
        if (daysWithEntries == 0)
        {
            return null;
        }

        if (!anyProfileReady)
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
