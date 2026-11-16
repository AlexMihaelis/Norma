using Norma.Core.Activity;

namespace Norma.Core.Tests.Activity;

/// <summary>
/// Контракт активности по MET. Ожидаемые калории посчитаны вручную по формуле
/// ккал/мин = MET x 3.5 x вес / 200: для 80 кг и 30 минут это MET x 42.
/// </summary>
public sealed class ActivityMathTests
{
    [Theory]
    [InlineData(ActivityKind.Walking, 4.3, 180.6)]
    [InlineData(ActivityKind.Running, 8.3, 348.6)]
    [InlineData(ActivityKind.Cycling, 6.8, 285.6)]
    [InlineData(ActivityKind.Swimming, 5.8, 243.6)]
    [InlineData(ActivityKind.StrengthTraining, 3.5, 147.0)]
    [InlineData(ActivityKind.Yoga, 2.5, 105.0)]
    [InlineData(ActivityKind.Dancing, 4.8, 201.6)]
    [InlineData(ActivityKind.Hiking, 6.0, 252.0)]
    [InlineData(ActivityKind.RowingMachine, 7.0, 294.0)]
    [InlineData(ActivityKind.DownhillSkiing, 5.3, 222.6)]
    public void Calories_for_80_kg_and_30_minutes(ActivityKind kind, double expectedMet, double expectedKcal)
    {
        Assert.Equal(expectedMet, ActivityMath.Met(kind), precision: 3);
        Assert.Equal(expectedKcal, ActivityMath.Calories(kind, weightKg: 80, minutes: 30), precision: 3);
    }

    [Theory]
    [InlineData(ActivityKind.Walking)]
    [InlineData(ActivityKind.Running)]
    [InlineData(ActivityKind.Cycling)]
    [InlineData(ActivityKind.Swimming)]
    [InlineData(ActivityKind.StrengthTraining)]
    [InlineData(ActivityKind.Yoga)]
    [InlineData(ActivityKind.Dancing)]
    [InlineData(ActivityKind.Hiking)]
    [InlineData(ActivityKind.RowingMachine)]
    [InlineData(ActivityKind.DownhillSkiing)]
    public void Calories_are_never_negative(ActivityKind kind)
    {
        // Критерий приёмки из PROJECT.md: отрицательных значений нет.
        Assert.True(ActivityMath.Calories(kind, weightKg: 80, minutes: 30) >= 0);
    }

    [Fact]
    public void Zero_minutes_means_zero_calories()
    {
        Assert.Equal(0, ActivityMath.Calories(ActivityKind.Running, weightKg: 80, minutes: 0), precision: 6);
    }

    [Fact]
    public void Negative_minutes_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ActivityMath.Calories(ActivityKind.Running, weightKg: 80, minutes: -10));
    }

    [Fact]
    public void Invalid_weight_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ActivityMath.Calories(ActivityKind.Running, weightKg: 0, minutes: 30));
    }

    [Fact]
    public void Unknown_activity_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ActivityMath.Met((ActivityKind)999));
    }

    [Fact]
    public void Total_sums_entries_for_the_day()
    {
        // Ходьба 30 мин (180.6) + йога 20 мин (2.5 x 1.4 x 20 = 70) = 250.6.
        var entries = new[]
        {
            new ActivityEntry(ActivityKind.Walking, 30),
            new ActivityEntry(ActivityKind.Yoga, 20),
        };

        Assert.Equal(250.6, ActivityMath.TotalCalories(entries, weightKg: 80), precision: 3);
    }

    [Fact]
    public void Total_of_empty_day_is_zero()
    {
        Assert.Equal(0, ActivityMath.TotalCalories([], weightKg: 80), precision: 6);
    }
}
