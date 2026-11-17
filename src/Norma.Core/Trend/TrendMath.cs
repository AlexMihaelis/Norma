namespace Norma.Core.Trend;

/// <summary>
/// Тренд веса: сглаживание EMA, скорость изменений и прогноз даты цели.
/// Чистые функции, без <c>DateTime.Now</c> — даты приходят в замерах.
/// </summary>
/// <remarks>
/// Зачем EMA, а не сырые замеры: вес скачет на воде, соли и еде на 1-2 кг за день,
/// и по сырому графику человек делает неверные выводы. EMA гасит шум, сохраняя
/// направление движения.
/// <list type="bullet">
/// <item>сглаживание: EMA с постоянной времени 10 дней, шаг учитывает пропуски
/// в датах; в день первого замера EMA равна самому замеру;</item>
/// <item>скорость: наклон линейной регрессии по EMA за последние 14 дней,
/// пересчитанный в кг/нед; меньше двух точек в окне - скорость 0;</item>
/// <item>прогноз: дата, когда текущий тренд дойдёт до цели; <c>null</c>, если
/// темп нулевой или направление не совпадает с целью.</item>
/// </list>
/// </remarks>
public static class TrendMath
{
    /// <summary>Постоянная времени сглаживания, дни.</summary>
    public const double DefaultTimeConstantDays = 10;

    /// <summary>Окно оценки скорости, дни: полтора цикла - волна гасится при любой фазе.</summary>
    public const int DefaultRateWindowDays = 42;

    /// <summary>Сглаживает замеры по EMA.</summary>
    /// <param name="entries">Замеры в произвольном порядке; сортируются по дате.</param>
    /// <param name="timeConstantDays">Постоянная времени: больше - плавнее.</param>
    public static IReadOnlyList<TrendPoint> Smooth(
        IEnumerable<WeightEntry> entries,
        double timeConstantDays = DefaultTimeConstantDays)
    {
        ArgumentNullException.ThrowIfNull(entries);

        if (timeConstantDays <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeConstantDays), timeConstantDays, "Постоянная времени должна быть положительной.");
        }

        var ordered = entries.OrderBy(entry => entry.Date).ToList();

        if (ordered.Count == 0)
        {
            return [];
        }

        foreach (var entry in ordered)
        {
            if (entry.Kg <= 0 || entry.Kg > 500)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(entries), entry.Kg, "Вес должен быть в диапазоне 0-500 кг.");
            }
        }

        var points = new List<TrendPoint>(ordered.Count);
        var ema = ordered[0].Kg;
        var previousDate = ordered[0].Date;
        points.Add(new TrendPoint(previousDate, ema));

        foreach (var entry in ordered.Skip(1))
        {
            var days = entry.Date.DayNumber - previousDate.DayNumber;
            var alpha = 1 - Math.Exp(-days / timeConstantDays);
            ema += alpha * (entry.Kg - ema);
            points.Add(new TrendPoint(entry.Date, ema));
            previousDate = entry.Date;
        }

        return points;
    }

    /// <summary>Скорость изменения веса за окно, кг/нед.</summary>
    /// <remarks>
    /// Считается по сырым замерам робастно (Тейл-Сен): сглаженная кривая отстаёт
    /// от тренда, а обычная регрессия ломается на выбросах и волнах.
    /// </remarks>
    public static double RatePerWeek(
        IReadOnlyList<WeightEntry> entries,
        int windowDays = DefaultRateWindowDays)
    {
        ArgumentNullException.ThrowIfNull(entries);

        if (windowDays <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(windowDays), windowDays, "Окно должно быть положительным.");
        }

        var trend = RobustTrend(entries, windowDays);

        return trend is null ? 0 : trend.Value.SlopePerDay * 7;
    }

    /// <summary>
    /// Робастная линия тренда: наклон - медиана парных наклонов (Тейл-Сен),
    /// уровень на последнюю дату - среднее остатков.
    /// </summary>
    /// <remarks>
    /// Медиана игнорирует одиночные выбросы (праздник, солёный ужин), а среднее
    /// остатков гасит периодическую волну (цикл), если окно не короче периода.
    /// Поэтому прогноз не уезжает от одного скачка и не переворачивает знак
    /// скорости на цикле.
    /// </remarks>
    private static (double SlopePerDay, double LevelAtLast)? RobustTrend(
        IReadOnlyList<WeightEntry> entries,
        int windowDays)
    {
        var ordered = entries.OrderBy(entry => entry.Date).ToList();

        if (ordered.Count < 2)
        {
            return null;
        }

        var lastDay = ordered[^1].Date.DayNumber;
        var window = ordered.Where(entry => lastDay - entry.Date.DayNumber <= windowDays).ToList();

        if (window.Count < 2)
        {
            return null;
        }

        var slopes = new List<double>();

        for (var i = 0; i < window.Count; i++)
        {
            for (var j = i + 1; j < window.Count; j++)
            {
                var days = window[j].Date.DayNumber - window[i].Date.DayNumber;

                if (days > 0)
                {
                    slopes.Add((window[j].Kg - window[i].Kg) / days);
                }
            }
        }

        if (slopes.Count == 0)
        {
            return null;
        }

        slopes.Sort();
        var slope = Median(slopes);

        var last = window[^1].Date.DayNumber;
        var level = window.Average(entry => entry.Kg - (slope * (entry.Date.DayNumber - last)));

        return (slope, level);
    }

    private static double Median(List<double> sorted)
    {
        var middle = sorted.Count / 2;

        return sorted.Count % 2 == 1
            ? sorted[middle]
            : (sorted[middle - 1] + sorted[middle]) / 2;
    }

    /// <summary>Дата, когда тренд дойдёт до цели; <c>null</c> — недостижимо.</summary>
    public static DateOnly? ForecastGoalDate(
        double currentEmaKg,
        DateOnly lastDate,
        double rateKgPerWeek,
        double goalKg)
    {
        var perDay = rateKgPerWeek / 7;

        // Скорость меньше ~0.01 кг/нед - это «стоим на месте», прогноз был бы
        // случайным числом на тысячи дней вперёд.
        if (Math.Abs(perDay) < 1e-4)
        {
            return null;
        }

        var difference = goalKg - currentEmaKg;

        // Цель не в ту сторону: худеем, а цель выше - не прогнозируем.
        if (difference * perDay <= 0)
        {
            return null;
        }

        var days = difference / perDay;

        return lastDate.AddDays((int)Math.Round(days, MidpointRounding.AwayFromZero));
    }

    /// <summary>Полный анализ: сглаживание, скорость и прогноз даты цели.</summary>
    /// <param name="goalKg">Цель, кг; без цели прогноза даты нет.</param>
    public static WeightTrend Analyze(
        IReadOnlyList<WeightEntry> entries,
        double? goalKg = null,
        double timeConstantDays = DefaultTimeConstantDays,
        int windowDays = DefaultRateWindowDays)
    {
        ArgumentNullException.ThrowIfNull(entries);

        if (entries.Count == 0)
        {
            throw new ArgumentException("Нужен хотя бы один замер веса.", nameof(entries));
        }

        if (goalKg is <= 0 or > 500)
        {
            throw new ArgumentOutOfRangeException(
                nameof(goalKg), goalKg, "Цель по весу должна быть в диапазоне 0-500 кг.");
        }

        var ordered = entries.OrderBy(entry => entry.Date).ToList();
        var points = Smooth(ordered, timeConstantDays);
        var last = points[^1];

        // Скорость и стартовый уровень прогноза - по робастной линии, а не по EMA:
        // EMA отстаёт, а обычная регрессия ломается на выбросах и волнах.
        var robust = RobustTrend(ordered, windowDays);
        var rate = robust is null ? 0 : robust.Value.SlopePerDay * 7;
        var forecastFrom = robust?.LevelAtLast ?? last.EmaKg;

        // Без цели считаем тренд и скорость, но не обещаем дату.
        var goalDate = goalKg is double goal
            ? ForecastGoalDate(forecastFrom, last.Date, rate, goal)
            : null;

        return new WeightTrend(points, last.EmaKg, rate, goalDate);
    }
}
