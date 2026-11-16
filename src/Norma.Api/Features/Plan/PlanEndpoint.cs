using Microsoft.AspNetCore.Http.HttpResults;
using Norma.Core.Energy;

namespace Norma.Api.Features.Plan;

/// <summary>
/// Срез «целевая норма по профилю». Первый срез, который отдаёт расчёт ядра по
/// HTTP: профиль приходит параметрами запроса, ответ — BMR, TDEE, цель и БЖУ.
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
            .WithName("Plan");

        return endpoints;
    }

    private static Results<Ok<PlanResponse>, ValidationProblem> Handle(
        string? sex,
        int? age,
        double? heightCm,
        double? weightKg,
        string? activity,
        string? goal,
        double? kgPerWeek)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        var parsedSex = ParseEnum<Sex>(sex, "sex", "female или male", errors);
        var parsedActivity = ParseEnum<ActivityLevel>(
            activity, "activity", "sedentary, light, moderate, active или veryActive", errors);
        var parsedGoal = ParseEnum<Goal>(goal, "goal", "lose, maintain или gain", errors);

        ValidateRange(age, "age", 14, 120, "Возраст должен быть в диапазоне 14–120 лет.", errors);
        ValidateRange(heightCm, "heightCm", 100, 250, "Рост должен быть в диапазоне 100–250 см.", errors);
        ValidateRange(weightKg, "weightKg", 30, 400, "Вес должен быть в диапазоне 30–400 кг.", errors);
        ValidateOptionalRange(kgPerWeek, "kgPerWeek", 0, 1.5, "Темп должен быть в диапазоне 0–1.5 кг/нед.", errors);

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var profile = new Profile(
            parsedSex!.Value,
            age!.Value,
            heightCm!.Value,
            weightKg!.Value,
            parsedActivity!.Value);

        var plan = EnergyMath.Plan(profile, parsedGoal!.Value, kgPerWeek ?? DefaultKgPerWeek);

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

    private static T? ParseEnum<T>(string? raw, string field, string expected, Dictionary<string, string[]> errors)
        where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            errors[field] = [$"Укажи {field}: {expected}."];
            return null;
        }

        if (!Enum.TryParse<T>(raw, ignoreCase: true, out var value) || !Enum.IsDefined(value))
        {
            errors[field] = [$"Недопустимое значение: ожидается {expected}."];
            return null;
        }

        return value;
    }

    private static void ValidateRange(
        double? value,
        string field,
        double min,
        double max,
        string message,
        Dictionary<string, string[]> errors)
    {
        if (value is null)
        {
            errors[field] = ["Параметр обязателен."];
        }
        else if (value < min || value > max)
        {
            errors[field] = [message];
        }
    }

    /// <summary>
    /// Проверка необязательного параметра: отсутствие значения — норма, тогда
    /// дальше применяется значение по умолчанию.
    /// </summary>
    private static void ValidateOptionalRange(
        double? value,
        string field,
        double min,
        double max,
        string message,
        Dictionary<string, string[]> errors)
    {
        if (value is not null && (value < min || value > max))
        {
            errors[field] = [message];
        }
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
