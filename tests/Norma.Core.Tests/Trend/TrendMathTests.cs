using Norma.Core.Trend;

namespace Norma.Core.Tests.Trend;

/// <summary>
/// Контракт тренда веса. Числа посчитаны независимо (отдельная реализация EMA на
/// Python) и закреплены до проверки; критерий приёмки из PROJECT.md - «шум не
/// двигает прогноз» - проверяется сравнением чистого и зашумлённого рядов.
/// </summary>
public sealed class TrendMathTests
{
    private static readonly DateOnly Start = new(2026, 11, 1);

    /// <summary>Ровное похудение: 0.5 кг в неделю, 31 ежедневный замер.</summary>
    private static List<WeightEntry> CleanSeries() =>
        Enumerable.Range(0, 31)
            .Select(i => new WeightEntry(Start.AddDays(i), 80 - (0.5 * i / 7)))
            .ToList();

    /// <summary>Тот же тренд, но с ежедневным шумом ±0.7 кг (вода, соль, еда).</summary>
    private static List<WeightEntry> NoisySeries() =>
        Enumerable.Range(0, 31)
            .Select(i => new WeightEntry(
                Start.AddDays(i),
                80 - (0.5 * i / 7) + (i % 2 == 0 ? 0.7 : -0.7)))
            .ToList();

    [Fact]
    public void Smooth_single_entry_returns_that_weight()
    {
        var points = TrendMath.Smooth([new WeightEntry(Start, 80)]);

        var point = Assert.Single(points);
        Assert.Equal(80, point.EmaKg, precision: 6);
        Assert.Equal(Start, point.Date);
    }

    [Fact]
    public void Smooth_uses_exponential_factor_between_two_entries()
    {
        // 7 дней при постоянной времени 10: alpha = 1 - exp(-0.7) = 0.503415,
        // EMA = 80 + 0.503415 × (79 - 80) = 79.496585.
        var points = TrendMath.Smooth(
        [
            new WeightEntry(Start, 80),
            new WeightEntry(Start.AddDays(7), 79),
        ]);

        Assert.Equal(2, points.Count);
        Assert.Equal(79.496585, points[1].EmaKg, precision: 4);
    }

    [Fact]
    public void Smooth_accounts_for_gaps_between_dates()
    {
        // Пропуски: 1 -> 3 дня (alpha = 0.181269) и 3 -> 8 дня (alpha = 0.393469).
        var points = TrendMath.Smooth(
        [
            new WeightEntry(Start, 80),
            new WeightEntry(Start.AddDays(2), 80.5),
            new WeightEntry(Start.AddDays(7), 79.6),
        ]);

        Assert.Equal(80, points[0].EmaKg, precision: 6);
        Assert.Equal(80.090635, points[1].EmaKg, precision: 4);
        Assert.Equal(79.897585, points[2].EmaKg, precision: 4);
    }

    [Fact]
    public void Smooth_sorts_entries_by_date()
    {
        var ordered = TrendMath.Smooth(
        [
            new WeightEntry(Start, 80),
            new WeightEntry(Start.AddDays(7), 79),
        ]);

        var reversed = TrendMath.Smooth(
        [
            new WeightEntry(Start.AddDays(7), 79),
            new WeightEntry(Start, 80),
        ]);

        Assert.Equal(ordered[1].EmaKg, reversed[1].EmaKg, precision: 6);
        Assert.Equal(Start, reversed[0].Date);
    }

    [Fact]
    public void Smooth_rejects_invalid_weight()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => TrendMath.Smooth([new WeightEntry(Start, 0)]));
    }

    [Fact]
    public void Rate_is_zero_for_single_point()
    {
        Assert.Equal(0, TrendMath.RatePerWeek([new WeightEntry(Start, 80)]), precision: 6);
    }

    [Fact]
    public void Rate_for_two_points_is_difference_per_week()
    {
        // Скорость считается по сырым замерам: 1 кг за 7 дней = -1 кг/нед.
        var entries = new[]
        {
            new WeightEntry(Start, 80),
            new WeightEntry(Start.AddDays(7), 79),
        };

        Assert.Equal(-1.0, TrendMath.RatePerWeek(entries), precision: 4);
    }

    [Fact]
    public void Analyze_clean_series_matches_expected_trend()
    {
        var trend = TrendMath.Analyze(CleanSeries(), goalKg: 77);

        Assert.Equal(78.502496, trend.CurrentEmaKg, precision: 4);
        Assert.Equal(-0.5, trend.RateKgPerWeek, precision: 4);
        Assert.NotNull(trend.GoalDate);
        Assert.Equal(new DateOnly(2026, 12, 13), trend.GoalDate.Value);
    }

    [Fact]
    public void Analyze_noisy_series_stays_close_to_real_rate()
    {
        var trend = TrendMath.Analyze(NoisySeries(), goalKg: 77);

        Assert.Equal(78.570576, trend.CurrentEmaKg, precision: 4);
        Assert.Equal(-0.5, trend.RateKgPerWeek, precision: 4);
        Assert.NotNull(trend.GoalDate);
        Assert.Equal(new DateOnly(2026, 12, 13), trend.GoalDate.Value);
    }

    [Fact]
    public void Celebration_spike_does_not_move_forecast()
    {
        // Праздник: один день +2 кг. Медианный наклон выброс игнорирует,
        // средний уровень сдвигается на 2/31 кг - прогноз почти не меняется.
        var withSpike = CleanSeries();
        withSpike[^1] = withSpike[^1] with { Kg = withSpike[^1].Kg + 2 };

        var clean = TrendMath.Analyze(CleanSeries(), goalKg: 77);
        var spike = TrendMath.Analyze(withSpike, goalKg: 77);

        Assert.Equal(-0.5, spike.RateKgPerWeek, precision: 2);
        Assert.NotNull(clean.GoalDate);
        Assert.NotNull(spike.GoalDate);

        var shiftDays = Math.Abs(spike.GoalDate.Value.DayNumber - clean.GoalDate.Value.DayNumber);

        Assert.True(shiftDays <= 3, $"Праздник сдвинул прогноз на {shiftDays} дней.");
    }

    [Fact]
    public void Cycle_wave_does_not_flip_rate_sign()
    {
        // Цикл: волна воды ±1 кг с периодом 28 дней поверх похудения.
        // Обычная регрессия на такой волне переворачивала знак скорости.
        var cycle = Enumerable.Range(0, 42)
            .Select(i => new WeightEntry(
                Start.AddDays(i),
                80 - (0.5 * i / 7) + Math.Sin(2 * Math.PI * i / 28)))
            .ToList();

        var trend = TrendMath.Analyze(cycle, goalKg: 77);

        Assert.True(trend.RateKgPerWeek < 0, $"Скорость должна быть отрицательной, а получилась {trend.RateKgPerWeek}.");
        Assert.NotNull(trend.GoalDate);
    }

    [Fact]
    public void Weight_gain_has_positive_rate_and_forecast()
    {
        // Набор массы: цель выше текущего веса, темп положительный.
        var gain = Enumerable.Range(0, 31)
            .Select(i => new WeightEntry(Start.AddDays(i), 70 + (0.5 * i / 7)))
            .ToList();

        var trend = TrendMath.Analyze(gain, goalKg: 74);

        Assert.Equal(0.5, trend.RateKgPerWeek, precision: 4);
        Assert.NotNull(trend.GoalDate);
        Assert.Equal(new DateOnly(2026, 12, 27), trend.GoalDate.Value);
    }

    [Fact]
    public void Noise_does_not_shift_forecast()
    {
        // Критерий приёмки: шум воды и еды не должен заметно двигать дату цели.
        var clean = TrendMath.Analyze(CleanSeries(), goalKg: 77);
        var noisy = TrendMath.Analyze(NoisySeries(), goalKg: 77);

        Assert.NotNull(clean.GoalDate);
        Assert.NotNull(noisy.GoalDate);

        var shiftDays = Math.Abs(noisy.GoalDate.Value.DayNumber - clean.GoalDate.Value.DayNumber);

        Assert.True(
            shiftDays <= 3,
            $"Прогноз сдвинулся на {shiftDays} дней: чистый {clean.GoalDate}, шумный {noisy.GoalDate}.");
    }

    [Fact]
    public void Analyze_without_goal_has_no_forecast()
    {
        // API запрашивает тренд и без цели: скорость и кривая есть, даты нет.
        var trend = TrendMath.Analyze(CleanSeries());

        Assert.Equal(78.502496, trend.CurrentEmaKg, precision: 4);
        Assert.Null(trend.GoalDate);
    }

    [Fact]
    public void Goal_in_the_wrong_direction_has_no_forecast()
    {
        // Худеем, а цель выше текущего тренда - прогноз невозможен.
        var trend = TrendMath.Analyze(CleanSeries(), goalKg: 81);

        Assert.Null(trend.GoalDate);
    }

    [Fact]
    public void Flat_series_has_no_forecast()
    {
        var flat = Enumerable.Range(0, 10)
            .Select(i => new WeightEntry(Start.AddDays(i), 80))
            .ToList();

        var trend = TrendMath.Analyze(flat, goalKg: 77);

        Assert.Equal(80, trend.CurrentEmaKg, precision: 6);
        Assert.Equal(0, trend.RateKgPerWeek, precision: 6);
        Assert.Null(trend.GoalDate);
    }

    [Fact]
    public void Analyze_rejects_empty_series()
    {
        Assert.Throws<ArgumentException>(() => TrendMath.Analyze([], goalKg: 77));
    }
}
