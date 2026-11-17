using Norma.Core.Energy;

namespace Norma.Core.Water;

/// <summary>
/// Дневной ориентир по воде: базовый объем из веса плюс поправка на активность.
/// <see cref="ClampedToMin"/> и <see cref="ClampedToMax"/> говорят, что границы
/// безопасности сработали и итог не равен сумме базы и поправки.
/// </summary>
public sealed record WaterTarget(
    int BaseMl,
    int ActivityBonusMl,
    int NormMl,
    bool ClampedToMin,
    bool ClampedToMax);

/// <summary>
/// Ориентир по воде. Это оценка, а не медицинская рекомендация: беременность,
/// лактация, болезни и выносливостные нагрузки в формулу не входят.
/// </summary>
public static class WaterMath
{
    /// <summary>Мужская норма на килограмм веса, мл.</summary>
    public const double MaleMlPerKg = 35;

    /// <summary>Женская норма на килограмм веса, мл.</summary>
    public const double FemaleMlPerKg = 31;

    /// <summary>Нижняя граница безопасности, мл в день.</summary>
    public const int MinMl = 1500;

    /// <summary>Верхняя граница безопасности, мл в день.</summary>
    public const int MaxMl = 4000;

    /// <summary>
    /// Считает ориентир по полу, весу и активности. Границы применяются к сумме
    /// «база + поправка», а не к базе: активный человек с малым весом тоже не
    /// должен уйти ниже безопасного минимума.
    /// </summary>
    public static WaterTarget ForProfile(Sex sex, double weightKg, ActivityLevel activity)
    {
        var mlPerKg = sex == Sex.Male ? MaleMlPerKg : FemaleMlPerKg;

        var baseMl = (int)Math.Round(mlPerKg * weightKg, MidpointRounding.AwayFromZero);
        var activityBonusMl = ActivityBonus(activity);
        var rawMl = baseMl + activityBonusMl;

        return new WaterTarget(
            baseMl,
            activityBonusMl,
            Math.Clamp(rawMl, MinMl, MaxMl),
            rawMl < MinMl,
            rawMl > MaxMl);
    }

    /// <summary>
    /// Прокси-поправка на повседневную активность, пока нет учета тренировок.
    /// Когда появится API активности, эта таблица заменится на «+500 мл за час».
    /// </summary>
    public static int ActivityBonus(ActivityLevel activity) => activity switch
    {
        ActivityLevel.Sedentary => 0,
        ActivityLevel.Light => 250,
        ActivityLevel.Moderate => 500,
        ActivityLevel.Active => 750,
        ActivityLevel.VeryActive => 1000,
        _ => throw new ArgumentOutOfRangeException(nameof(activity), activity, "Неизвестный уровень активности."),
    };
}
