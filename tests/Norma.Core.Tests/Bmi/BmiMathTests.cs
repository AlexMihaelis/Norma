using Norma.Core.Bmi;

namespace Norma.Core.Tests.Bmi;

/// <summary>
/// ИМТ и весовые зоны. Пороги фиксированы до реализации: это границы ВОЗ,
/// а не подгонка под пример.
/// </summary>
public sealed class BmiMathTests
{
    [Fact]
    public void Value_is_weight_over_height_squared()
    {
        // 70 / 1.75² = 70 / 3.0625 = 22.857...
        Assert.Equal(22.857, BmiMath.Value(70, 175), precision: 3);
    }

    [Theory]
    [InlineData(18.4, BmiCategory.Low)]
    [InlineData(18.5, BmiCategory.Normal)]
    [InlineData(24.9, BmiCategory.Normal)]
    [InlineData(25.0, BmiCategory.High)]
    [InlineData(29.9, BmiCategory.High)]
    [InlineData(30.0, BmiCategory.VeryHigh)]
    public void Category_uses_who_adult_thresholds(double bmi, BmiCategory expected)
    {
        Assert.Equal(expected, BmiMath.CategoryFor(bmi));
    }

    [Fact]
    public void Healthy_weight_range_for_175_cm()
    {
        // 18.5 × 3.0625 = 56.65625; 25 × 3.0625 = 76.5625; 30 × 3.0625 = 91.875.
        Assert.Equal(56.65625, BmiMath.HealthyWeightMinKg(175), precision: 5);
        Assert.Equal(76.5625, BmiMath.HealthyWeightMaxKg(175), precision: 5);
        Assert.Equal(91.875, BmiMath.ObeseFromKg(175), precision: 5);
    }

    [Fact]
    public void For_measurement_combines_value_category_and_zones()
    {
        var normal = BmiMath.ForMeasurement(70, 175);
        Assert.Equal(BmiCategory.Normal, normal.Category);

        // 95 кг при 175 см - ИМТ 31.0, «очень высокий».
        var obese = BmiMath.ForMeasurement(95, 175);
        Assert.Equal(BmiCategory.VeryHigh, obese.Category);
        Assert.True(obese.Value > BmiMath.VeryHighFrom);
    }

    [Fact]
    public void Range_boundaries_match_bmi_thresholds()
    {
        // Вес на границах соответствует ровно 18.5 / 25 / 30: зоны на графике
        // не разъезжаются с категориями.
        var min = BmiMath.HealthyWeightMinKg(175);
        var max = BmiMath.HealthyWeightMaxKg(175);

        Assert.Equal(BmiMath.LowBelow, BmiMath.Value(min, 175), precision: 10);
        Assert.Equal(BmiMath.HighFrom, BmiMath.Value(max, 175), precision: 10);
    }

    [Fact]
    public void Zero_height_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BmiMath.Value(70, 0));
    }
}
