using Norma.Core.Energy;
using Norma.Core.Water;

namespace Norma.Core.Tests.Water;

/// <summary>
/// Контракт ориентира по воде: пол задает норму на килограмм, активность -
/// фиксированную прибавку, границы безопасности применяются последними.
/// </summary>
public sealed class WaterMathTests
{
    [Fact]
    public void Male_norm_is_35_ml_per_kg()
    {
        var target = WaterMath.ForProfile(Sex.Male, 80, ActivityLevel.Sedentary);

        Assert.Equal(2800, target.BaseMl);
        Assert.Equal(0, target.ActivityBonusMl);
        Assert.Equal(2800, target.NormMl);
        Assert.False(target.ClampedToMin);
        Assert.False(target.ClampedToMax);
    }

    [Fact]
    public void Female_norm_is_31_ml_per_kg()
    {
        var target = WaterMath.ForProfile(Sex.Female, 70, ActivityLevel.Sedentary);

        Assert.Equal(2170, target.BaseMl);
        Assert.Equal(2170, target.NormMl);
    }

    [Fact]
    public void Base_volume_is_rounded_away_from_zero()
    {
        // 31 × 70.5 = 2185.5 - при округлении к ближайшему получаем 2186.
        var target = WaterMath.ForProfile(Sex.Female, 70.5, ActivityLevel.Sedentary);

        Assert.Equal(2186, target.BaseMl);
    }

    [Theory]
    [InlineData(ActivityLevel.Sedentary, 0)]
    [InlineData(ActivityLevel.Light, 250)]
    [InlineData(ActivityLevel.Moderate, 500)]
    [InlineData(ActivityLevel.Active, 750)]
    [InlineData(ActivityLevel.VeryActive, 1000)]
    public void Activity_adds_fixed_millilitres(ActivityLevel activity, int expectedBonus)
    {
        // Женский базовый объем 31 × 60 = 1860 - с любой поправкой не упирается
        // в границы, поэтому итог равен базе плюс прибавка.
        var target = WaterMath.ForProfile(Sex.Female, 60, activity);

        Assert.Equal(expectedBonus, target.ActivityBonusMl);
        Assert.Equal(1860 + expectedBonus, target.NormMl);
        Assert.False(target.ClampedToMin);
        Assert.False(target.ClampedToMax);
    }

    [Fact]
    public void Low_norm_is_raised_to_safety_floor()
    {
        // 31 × 40 = 1240 меньше нижней границы 1500.
        var target = WaterMath.ForProfile(Sex.Female, 40, ActivityLevel.Sedentary);

        Assert.Equal(1240, target.BaseMl);
        Assert.Equal(WaterMath.MinMl, target.NormMl);
        Assert.True(target.ClampedToMin);
        Assert.False(target.ClampedToMax);
    }

    [Fact]
    public void High_norm_is_capped()
    {
        // 35 × 120 + 1000 = 5200 больше верхней границы 4000.
        var target = WaterMath.ForProfile(Sex.Male, 120, ActivityLevel.VeryActive);

        Assert.Equal(4200, target.BaseMl);
        Assert.Equal(1000, target.ActivityBonusMl);
        Assert.Equal(WaterMath.MaxMl, target.NormMl);
        Assert.True(target.ClampedToMax);
        Assert.False(target.ClampedToMin);
    }

    [Fact]
    public void Bounds_are_inclusive()
    {
        // 1500 мл ровно: нижняя граница достигнута, но это не «урезание».
        var low = WaterMath.ForProfile(Sex.Female, 1500 / WaterMath.FemaleMlPerKg, ActivityLevel.Sedentary);
        Assert.Equal(WaterMath.MinMl, low.NormMl);
        Assert.False(low.ClampedToMin);

        var high = WaterMath.ForProfile(Sex.Male, 4000 / WaterMath.MaleMlPerKg, ActivityLevel.Sedentary);
        Assert.Equal(WaterMath.MaxMl, high.NormMl);
        Assert.False(high.ClampedToMax);
    }
}
