namespace Norma.Core.Energy;

/// <summary>
/// Расчёт целевых БЖУ от готовой калорийности. Чистые функции, как и
/// <see cref="EnergyMath"/>.
/// </summary>
/// <remarks>
/// Политика (закреплена тестами в <c>MacroMathTests</c>):
/// <list type="bullet">
/// <item>белок — 1.8 г/кг текущего веса в дефиците/профиците и 1.6 г/кг при
/// поддержании: в дефиците белок защищает мышцы, и брать его от текущего веса —
/// единственный корректный вариант, пока мы не знаем процент жира;</item>
/// <item>белок ограничен 35 % калорий: иначе у очень тяжёлых людей он съедал бы всю
/// норму и углеводов оставалось бы меньше нуля;</item>
/// <item>жиры — 25 % калорий (стандарт для гормонов и усвоения витаминов);</item>
/// <item>углеводы — остаток; отрицательными не бывают благодаря ограничению белка.</item>
/// </list>
/// </remarks>
public static class MacroMath
{
    /// <summary>Белок в дефиците и профиците, г/кг веса.</summary>
    public const double ProteinPerKgOnGoal = 1.8;

    /// <summary>Белок при поддержании, г/кг веса.</summary>
    public const double ProteinPerKgMaintain = 1.6;

    /// <summary>Потолок белка в калориях — 35 %.</summary>
    public const double MaxProteinCalorieShare = 0.35;

    /// <summary>Доля калорий из жиров — 25 %.</summary>
    public const double FatCalorieShare = 0.25;

    private const double KcalPerGramProtein = 4;
    private const double KcalPerGramFat = 9;
    private const double KcalPerGramCarbs = 4;

    /// <summary>Целевые БЖУ для заданной калорийности и профиля.</summary>
    public static MacroPlan ForTarget(Profile profile, Goal goal, int targetKcal)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (targetKcal <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetKcal), targetKcal, "Калорийность должна быть положительной.");
        }

        var proteinPerKg = goal == Goal.Maintain ? ProteinPerKgMaintain : ProteinPerKgOnGoal;
        var proteinFromWeight = proteinPerKg * profile.WeightKg;
        var proteinCap = targetKcal * MaxProteinCalorieShare / KcalPerGramProtein;
        var proteinG = Math.Min(proteinFromWeight, proteinCap);

        var fatKcal = targetKcal * FatCalorieShare;
        var carbsKcal = Math.Max(0, targetKcal - (proteinG * KcalPerGramProtein) - fatKcal);

        return new MacroPlan(
            ProteinG: Round(proteinG),
            FatG: Round(fatKcal / KcalPerGramFat),
            CarbsG: Round(carbsKcal / KcalPerGramCarbs));
    }

    private static int Round(double grams) => (int)Math.Round(grams, MidpointRounding.AwayFromZero);
}
