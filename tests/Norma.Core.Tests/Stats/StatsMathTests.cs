using Norma.Core.Stats;

namespace Norma.Core.Tests.Stats;

/// <summary>
/// Контракт статистики: среднее по дням с записями и отклонение от цели.
/// Ожидаемые числа посчитаны вручную.
/// </summary>
public sealed class StatsMathTests
{
    [Fact]
    public void Average_uses_only_provided_days()
    {
        // Пустые дни срез не передает, поэтому среднее - по двум дням с едой.
        var days = new[]
        {
            new DayIntake(new DateOnly(2026, 11, 17), 1800, 90, 60, 200, 4),
            new DayIntake(new DateOnly(2026, 11, 18), 2200, 110, 70, 260, 5),
        };

        var averages = StatsMath.Average(days);

        Assert.Equal(2000, averages.Kcal, precision: 6);
        Assert.Equal(100, averages.Protein, precision: 6);
        Assert.Equal(65, averages.Fat, precision: 6);
        Assert.Equal(230, averages.Carbs, precision: 6);
    }

    [Fact]
    public void Average_of_empty_period_is_zero()
    {
        var averages = StatsMath.Average([]);

        Assert.Equal(0, averages.Kcal, precision: 6);
        Assert.Equal(0, averages.Protein, precision: 6);
    }

    [Fact]
    public void Average_rounds_to_one_decimal()
    {
        // 100 и 101 в среднем 100.5; 10.05 и 10.15 дадут 10.1 (половина вверх).
        var days = new[]
        {
            new DayIntake(new DateOnly(2026, 11, 17), 100, 10.05, 0, 0, 1),
            new DayIntake(new DateOnly(2026, 11, 18), 101, 10.15, 0, 0, 1),
        };

        var averages = StatsMath.Average(days);

        Assert.Equal(100.5, averages.Kcal, precision: 6);
        Assert.Equal(10.1, averages.Protein, precision: 6);
    }

    [Fact]
    public void Deviation_is_negative_when_eating_below_target()
    {
        // 1800 против 2000: -200 ккал, -10 %.
        var deviation = StatsMath.Deviation(averageKcal: 1800, averageTargetKcal: 2000);

        Assert.Equal(2000, deviation.TargetKcal);
        Assert.Equal(-200, deviation.DiffKcal);
        Assert.Equal(-10.0, deviation.DiffPercent, precision: 6);
    }

    [Fact]
    public void Deviation_is_positive_when_eating_above_target()
    {
        var deviation = StatsMath.Deviation(averageKcal: 2200, averageTargetKcal: 2000);

        Assert.Equal(200, deviation.DiffKcal);
        Assert.Equal(10.0, deviation.DiffPercent, precision: 6);
    }

    [Fact]
    public void Deviation_rejects_non_positive_target()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => StatsMath.Deviation(averageKcal: 1800, averageTargetKcal: 0));
    }
}
