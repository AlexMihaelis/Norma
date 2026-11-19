using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Norma.Api.Data;
using Norma.Api.Features.Auth;
using Norma.Core.Energy;

namespace Norma.Api.Features.Plan;

/// <summary>
/// Срез «целевая норма по профилю»: BMR, TDEE, цель и БЖУ по сохраненному
/// профилю и последнему весу. Не хватает данных - ответ перечисляет, что
/// заполнить, вместо выдуманных чисел.
/// </summary>
/// <remarks>
/// Папка среза: контракт ответа и регистрация маршрута рядом. Валидация — здесь
/// же: срез не вызывает другие срезы, а бизнес-правила живут в <c>Core</c>.
/// </remarks>
public static class PlanEndpoint
{
    private const double DefaultKgPerWeek = 0.25;

    public static IEndpointRouteBuilder MapPlan(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet("/plan", Handle)
            .WithName("Plan")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<Results<Ok<PlanResponse>, ValidationProblem, UnauthorizedHttpResult>> Handle(
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

        var profile = await db.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken);

        // Норма считается от последнего замера: дата важнее, чем порядок ввода.
        var weightKg = await db.WeightMeasurements
            .Where(measurement => measurement.UserId == userId)
            .OrderByDescending(measurement => measurement.Date)
            .ThenByDescending(measurement => measurement.CreatedAt)
            .Select(measurement => (double?)measurement.Kg)
            .FirstOrDefaultAsync(cancellationToken);

        var sex = profile?.Sex;
        var birthDate = profile?.BirthDate;
        var heightCm = profile?.HeightCm;
        var activity = profile?.Activity;
        var goal = profile?.Goal;

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (sex is null)
        {
            errors["sex"] = ["Укажите пол в профиле."];
        }

        if (birthDate is null)
        {
            errors["birthDate"] = ["Укажите дату рождения в профиле."];
        }

        if (heightCm is null)
        {
            errors["heightCm"] = ["Укажите рост в профиле."];
        }

        if (activity is null)
        {
            errors["activity"] = ["Укажите уровень активности в профиле."];
        }

        if (goal is null)
        {
            errors["goal"] = ["Укажите цель по весу в профиле."];
        }

        if (weightKg is null)
        {
            errors["weight"] = ["Запишите вес - план считается от последнего замера."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors, title: "Профиль не заполнен");
        }

        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

        var coreProfile = ProfileBuilder.From(
            sex!.Value,
            birthDate!.Value,
            today,
            heightCm!.Value,
            weightKg!.Value,
            activity!.Value);

        // Темп необязателен: без него берем тот же по умолчанию, что раньше
        // принимал срез в query-параметрах.
        var plan = EnergyMath.Plan(coreProfile, goal!.Value, profile!.KgPerWeek ?? DefaultKgPerWeek);

        return TypedResults.Ok(new PlanResponse(
            Bmr: plan.Bmr,
            Tdee: plan.Tdee,
            TargetKcal: plan.TargetKcal,
            PaceAchievable: plan.PaceAchievable,
            BelowBmr: plan.BelowBmr,
            Macros: new MacroResponse(
                plan.Macros.ProteinG,
                plan.Macros.FatG,
                plan.Macros.CarbsG)));
    }
}

/// <summary>Ответ среза: все промежуточные числа, из которых сложилась цель.</summary>
public sealed record PlanResponse(
    double Bmr,
    double Tdee,
    int TargetKcal,
    bool PaceAchievable,
    bool BelowBmr,
    MacroResponse Macros);

/// <summary>Целевые БЖУ, граммы.</summary>
public sealed record MacroResponse(int ProteinG, int FatG, int CarbsG);
