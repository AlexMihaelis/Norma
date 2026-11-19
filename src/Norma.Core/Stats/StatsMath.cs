namespace Norma.Core.Stats;

/// <summary>Съеденное за день: КБЖУ, посчитанное из продуктов, и число позиций.</summary>
public sealed record DayIntake(
    DateOnly Date,
    double Kcal,
    double Protein,
    double Fat,
    double Carbs,
    int Entries);

/// <summary>Средние величины за период. Считаются только по дням с записями.</summary>
public sealed record IntakeAverages(double Kcal, double Protein, double Fat, double Carbs);

/// <summary>Отклонение факта от цели: разница в ккал и в процентах от цели.</summary>
public sealed record TargetDeviation(int TargetKcal, int DiffKcal, double DiffPercent);

/// <summary>
/// Арифметика статистики. Чистые функции: данные приходят списком, время не нужно.
/// </summary>
/// <remarks>
/// Среднее - только по дням с записями: пустой день не должен занижать «типичный
/// день».
/// </remarks>
public static class StatsMath
{
    public static IntakeAverages Average(IReadOnlyList<DayIntake> days)
    {
        ArgumentNullException.ThrowIfNull(days);

        if (days.Count == 0)
        {
            return new IntakeAverages(0, 0, 0, 0);
        }

        return new IntakeAverages(
            Round(days.Average(day => day.Kcal)),
            Round(days.Average(day => day.Protein)),
            Round(days.Average(day => day.Fat)),
            Round(days.Average(day => day.Carbs)));
    }

    /// <summary>
    /// Отклонение среднего факта от средней цели. Положительное значение - ел выше
    /// цели, отрицательное - ниже.
    /// </summary>
    public static TargetDeviation Deviation(double averageKcal, double averageTargetKcal)
    {
        if (averageTargetKcal <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(averageTargetKcal), averageTargetKcal, "Цель должна быть положительной.");
        }

        var diff = averageKcal - averageTargetKcal;

        return new TargetDeviation(
            TargetKcal: (int)Math.Round(averageTargetKcal, MidpointRounding.AwayFromZero),
            DiffKcal: (int)Math.Round(diff, MidpointRounding.AwayFromZero),
            DiffPercent: Math.Round(diff / averageTargetKcal * 100, 1, MidpointRounding.AwayFromZero));
    }

    /// <summary>Один знак после запятой: граммы в ответе не дробим мельче.</summary>
    private static double Round(double value) =>
        Math.Round(value, 1, MidpointRounding.AwayFromZero);
}
