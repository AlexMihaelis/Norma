namespace Norma.Core.Energy;

/// <summary>
/// Расчёты нормы калорий. Чистые функции: без EF, DI и <c>DateTime.Now</c> —
/// всё, что зависит от времени, приходит параметрами.
/// </summary>
/// <remarks>
/// Политика цели (закреплена тестами в <c>EnergyMathTests</c>):
/// <list type="bullet">
/// <item>похудение: дефицит = 7700 ккал × кг/нед / 7 (7700 ккал ≈ 1 кг жира);</item>
/// <item>набор: профицит по той же формуле, поддержание — без поправки;</item>
/// <item>безопасный минимум — 1200 ккал для женщин и 1500 для мужчин: ниже него
/// цель не опускается, а темп помечается недостижимым;</item>
/// <item>между минимумом и BMR цель допустима, но помечается
/// <see cref="CaloriePlan.BelowBmr"/> — это зона, где нужен предупреждающий текст,
/// а не молчаливая цифра;</item>
/// <item>целевая калорийность округляется до 10 ккал, BMR и TDEE отдаются точно.</item>
/// </list>
/// </remarks>
public static class EnergyMath
{
    /// <summary>Энергия одного килограмма жировой массы, ккал.</summary>
    public const double KcalPerKg = 7700;

    /// <summary>Безопасный минимум калорийности для женщин, ккал/сут.</summary>
    public const int FemaleSafetyFloorKcal = 1200;

    /// <summary>Безопасный минимум калорийности для мужчин, ккал/сут.</summary>
    public const int MaleSafetyFloorKcal = 1500;

    /// <summary>
    /// Основной обмен по формуле Миффлина — Сан Жеора, ккал/сут.
    /// </summary>
    /// <remarks>
    /// Мужчины: 10×вес + 6.25×рост − 5×возраст + 5.
    /// Женщины: 10×вес + 6.25×рост − 5×возраст − 161.
    /// </remarks>
    public static double BasalMetabolicRate(Sex sex, int ageYears, double heightCm, double weightKg)
    {
        var baseline = (10 * weightKg) + (6.25 * heightCm) - (5 * ageYears);

        return sex == Sex.Male ? baseline + 5 : baseline - 161;
    }

    /// <summary>Множитель активности: сидячий 1.2 … очень высокий 1.9.</summary>
    public static double ActivityFactor(ActivityLevel level) => level switch
    {
        ActivityLevel.Sedentary => 1.2,
        ActivityLevel.Light => 1.375,
        ActivityLevel.Moderate => 1.55,
        ActivityLevel.Active => 1.725,
        ActivityLevel.VeryActive => 1.9,
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Неизвестный уровень активности."),
    };

    /// <summary>Суточный расход: BMR × множитель активности, ккал/сут.</summary>
    public static double TotalDailyEnergyExpenditure(double bmr, ActivityLevel level) =>
        bmr * ActivityFactor(level);

    /// <summary>Итоговый план: BMR → TDEE → цель с учётом темпа.</summary>
    /// <param name="profile">Профиль пользователя.</param>
    /// <param name="goal">Цель по весу.</param>
    /// <param name="kgPerWeek">Желаемый темп, кг/нед; для поддержания — 0.</param>
    public static CaloriePlan Plan(Profile profile, Goal goal, double kgPerWeek)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (kgPerWeek < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(kgPerWeek), kgPerWeek, "Темп не может быть отрицательным: направление задаёт цель.");
        }

        var bmr = BasalMetabolicRate(profile.Sex, profile.AgeYears, profile.HeightCm, profile.WeightKg);
        var tdee = TotalDailyEnergyExpenditure(bmr, profile.Activity);
        var dailyDelta = KcalPerKg * kgPerWeek / 7;

        var rawTarget = goal switch
        {
            Goal.Lose => tdee - dailyDelta,
            Goal.Gain => tdee + dailyDelta,
            _ => tdee,
        };

        // Ниже безопасного минимума не опускаемся никогда: прижимаем цель к полу
        // и честно говорим, что запрошенный темп недостижим.
        var safetyFloor = profile.Sex == Sex.Female ? FemaleSafetyFloorKcal : MaleSafetyFloorKcal;
        var target = goal == Goal.Lose && rawTarget < safetyFloor ? safetyFloor : rawTarget;

        var roundedTarget = (int)(Math.Round(target / 10, MidpointRounding.AwayFromZero) * 10);

        return new CaloriePlan(
            Bmr: bmr,
            Tdee: tdee,
            TargetKcal: roundedTarget,
            PaceAchievable: Math.Abs(roundedTarget - rawTarget) <= 5,
            BelowBmr: roundedTarget < bmr,
            Macros: MacroMath.ForTarget(profile, goal, roundedTarget));
    }
}
