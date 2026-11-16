namespace Norma.Core.Activity;

/// <summary>
/// Расход калорий на активность по MET-таблицам. Чистые функции: вес и минуты
/// приходят параметрами, текущее время не нужно.
/// </summary>
/// <remarks>
/// Формула из Compendium of Physical Activities: ккал/мин = MET × 3.5 × вес / 200.
/// Значения MET взяты для умеренной интенсивности; это оценка, а не измерение,
/// поэтому UI обязан показывать её как «примерно».
/// </remarks>
public static class ActivityMath
{
    /// <summary>Коэффициент формулы: 3.5 мл O2 на кг в минуту, 200 — перевод в ккал.</summary>
    private const double FormulaFactor = 3.5 / 200;

    /// <summary>MET-значение вида активности (умеренная интенсивность).</summary>
    public static double Met(ActivityKind kind) => kind switch
    {
        ActivityKind.Walking => 4.3,        // быстрая ходьба ~5.6 км/ч
        ActivityKind.Running => 8.3,        // бег ~8 км/ч
        ActivityKind.Cycling => 6.8,        // велосипед 16-19 км/ч
        ActivityKind.Swimming => 5.8,       // плавание, умеренно
        ActivityKind.StrengthTraining => 3.5, // силовая тренировка
        ActivityKind.Yoga => 2.5,           // хатха-йога
        ActivityKind.Dancing => 4.8,        // танцы, общие
        ActivityKind.Hiking => 6.0,         // пеший поход по пересечённой местности
        ActivityKind.RowingMachine => 7.0,  // гребной тренажёр
        ActivityKind.DownhillSkiing => 5.3, // горные лыжи, умеренно
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Неизвестный вид активности."),
    };

    /// <summary>Калории за сессию активности.</summary>
    /// <param name="kind">Вид активности.</param>
    /// <param name="weightKg">Вес пользователя, кг.</param>
    /// <param name="minutes">Длительность, минуты.</param>
    public static double Calories(ActivityKind kind, double weightKg, int minutes)
    {
        if (weightKg is <= 0 or > 500)
        {
            throw new ArgumentOutOfRangeException(
                nameof(weightKg), weightKg, "Вес должен быть в диапазоне 0-500 кг.");
        }

        if (minutes is < 0 or > 1440)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minutes), minutes, "Длительность должна быть в диапазоне 0-1440 минут.");
        }

        return Met(kind) * FormulaFactor * weightKg * minutes;
    }

    /// <summary>Суммарный расход за набор записей.</summary>
    public static double TotalCalories(IEnumerable<ActivityEntry> entries, double weightKg)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var total = 0.0;

        foreach (var entry in entries)
        {
            total += Calories(entry.Kind, weightKg, entry.Minutes);
        }

        return total;
    }
}
