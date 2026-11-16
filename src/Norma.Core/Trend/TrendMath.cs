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

    /// <summary>Окно оценки скорости, дни.</summary>
    public const int DefaultRateWindowDays = 14;

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

    /// <summary>Скорость изменения EMA за окно, кг/нед.</summary>
    public static double RatePerWeek(
        IReadOnlyList<TrendPoint> points,
        int windowDays = DefaultRateWindowDays)
    {
        ArgumentNullException.ThrowIfNull(points);

        if (windowDays <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(windowDays), windowDays, "Окно должно быть положительным.");
        }

        if (points.Count < 2)
        {
            return 0;
        }

        var lastDay = points[^1].Date.DayNumber;
        var window = points.Where(point => lastDay - point.Date.DayNumber <= windowDays).ToList();

        if (window.Count < 2)
        {
            return 0;
        }

        var firstDay = window[0].Date.DayNumber;
        var meanX = window.Average(point => point.Date.DayNumber - firstDay);
        var meanY = window.Average(point => point.EmaKg);

        double covariance = 0;
        double variance = 0;

        foreach (var point in window)
        {
            var dx = point.Date.DayNumber - firstDay - meanX;
            covariance += dx * (point.EmaKg - meanY);
            variance += dx * dx;
        }

        return variance == 0 ? 0 : covariance / variance * 7;
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
    public static WeightTrend Analyze(
        IReadOnlyList<WeightEntry> entries,
        double goalKg,
        double timeConstantDays = DefaultTimeConstantDays,
        int windowDays = DefaultRateWindowDays)
    {
        ArgumentNullException.ThrowIfNull(entries);

        if (entries.Count == 0)
        {
            throw new ArgumentException("Нужен хотя бы один замер веса.", nameof(entries));
        }

        if (goalKg <= 0 || goalKg > 500)
        {
            throw new ArgumentOutOfRangeException(
                nameof(goalKg), goalKg, "Цель по весу должна быть в диапазоне 0-500 кг.");
        }

        var points = Smooth(entries, timeConstantDays);
        var last = points[^1];
        var rate = RatePerWeek(points, windowDays);
        var goalDate = ForecastGoalDate(last.EmaKg, last.Date, rate, goalKg);

        return new WeightTrend(points, last.EmaKg, rate, goalDate);
    }
}
