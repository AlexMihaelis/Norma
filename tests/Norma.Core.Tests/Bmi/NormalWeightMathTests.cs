using Norma.Core.Bmi;

namespace Norma.Core.Tests.Bmi;

/// <summary>
/// Норма веса с поправкой на возраст: 18-64 года - 18.5-25, 65+ - 22-27.
/// Пол на границы не влияет, дети и подростки - вне scope.
/// </summary>
public sealed class NormalWeightMathTests
{
    [Theory]
    [InlineData(null, WeightAgeBand.Unknown)]
    [InlineData(17, WeightAgeBand.Minor)]
    [InlineData(18, WeightAgeBand.Adult)]
    [InlineData(64, WeightAgeBand.Adult)]
    [InlineData(65, WeightAgeBand.OlderAdult)]
    [InlineData(90, WeightAgeBand.OlderAdult)]
    public void Band_is_chosen_by_age(int? age, WeightAgeBand expected)
    {
        Assert.Equal(expected, NormalWeightMath.BandFor(age));
    }

    [Fact]
    public void Adult_range_for_175_cm()
    {
        // 18.5 × 3.0625 = 56.65625; 25 × 3.0625 = 76.5625.
        var range = NormalWeightMath.For(30, 175);

        Assert.NotNull(range);
        Assert.Equal(56.65625, range.MinKg, precision: 5);
        Assert.Equal(76.5625, range.MaxKg, precision: 5);
        Assert.Equal(18.5, range.BmiMin);
        Assert.Equal(25, range.BmiMax);
        Assert.Equal(WeightAgeBand.Adult, range.AgeBand);
    }

    [Fact]
    public void Older_adult_range_is_higher()
    {
        // 22 × 3.0625 = 67.375; 27 × 3.0625 = 82.6875.
        var range = NormalWeightMath.For(70, 175);

        Assert.NotNull(range);
        Assert.Equal(67.375, range.MinKg, precision: 5);
        Assert.Equal(82.6875, range.MaxKg, precision: 5);
        Assert.Equal(22, range.BmiMin);
        Assert.Equal(27, range.BmiMax);
        Assert.Equal(WeightAgeBand.OlderAdult, range.AgeBand);
    }

    [Fact]
    public void Unknown_age_falls_back_to_adult_range()
    {
        var range = NormalWeightMath.For(null, 175);

        Assert.NotNull(range);
        Assert.Equal(WeightAgeBand.Unknown, range.AgeBand);
        Assert.Equal(56.65625, range.MinKg, precision: 5);
        Assert.Equal(76.5625, range.MaxKg, precision: 5);
    }

    [Fact]
    public void Minor_has_no_range()
    {
        Assert.Null(NormalWeightMath.For(17, 175));
    }

    [Theory]
    [InlineData(18.4, BmiCategory.Low)]
    [InlineData(18.5, BmiCategory.Normal)]
    [InlineData(24.9, BmiCategory.Normal)]
    [InlineData(25.0, BmiCategory.High)]
    [InlineData(30.0, BmiCategory.VeryHigh)]
    public void Adult_category_matches_who_thresholds(double bmi, BmiCategory expected)
    {
        var range = NormalWeightMath.For(30, 175);

        Assert.NotNull(range);
        Assert.Equal(expected, NormalWeightMath.CategoryFor(bmi, range));
    }

    [Theory]
    [InlineData(21.9, BmiCategory.Low)]
    [InlineData(22.0, BmiCategory.Normal)]
    [InlineData(26.9, BmiCategory.Normal)]
    [InlineData(27.0, BmiCategory.High)]
    [InlineData(29.9, BmiCategory.High)]
    [InlineData(30.0, BmiCategory.VeryHigh)]
    public void Older_adult_category_uses_shifted_band(double bmi, BmiCategory expected)
    {
        var range = NormalWeightMath.For(70, 175);

        Assert.NotNull(range);
        Assert.Equal(expected, NormalWeightMath.CategoryFor(bmi, range));
    }

    [Fact]
    public void Range_kg_boundaries_match_bmi_boundaries()
    {
        var range = NormalWeightMath.For(70, 175);

        Assert.NotNull(range);
        Assert.Equal(range.BmiMin, BmiMath.Value(range.MinKg, 175), precision: 10);
        Assert.Equal(range.BmiMax, BmiMath.Value(range.MaxKg, 175), precision: 10);
    }

    [Fact]
    public void Zero_height_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NormalWeightMath.For(30, 0));
    }
}
