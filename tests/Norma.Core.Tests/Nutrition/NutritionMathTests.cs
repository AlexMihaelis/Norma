using Norma.Core.Nutrition;

namespace Norma.Core.Tests.Nutrition;

/// <summary>
/// Контракт пересчета КБЖУ на количество. Ожидаемые числа посчитаны вручную:
/// значение на 100 × количество / 100.
/// </summary>
public sealed class NutritionMathTests
{
    private static readonly NutritionValues ChickenPer100 = new(165, 31, 3.6, 0);

    [Fact]
    public void Portion_scales_all_values()
    {
        // 150 г грудки: 165 × 1.5 = 247.5 ккал, 31 × 1.5 = 46.5 г белка.
        var portion = NutritionMath.ForQuantity(ChickenPer100, 150);

        Assert.Equal(247.5, portion.Kcal, precision: 6);
        Assert.Equal(46.5, portion.Protein, precision: 6);
        Assert.Equal(5.4, portion.Fat, precision: 6);
        Assert.Equal(0, portion.Carbs, precision: 6);
    }

    [Fact]
    public void Milliliters_use_the_same_formula()
    {
        // Молоко 60 ккал на 100 мл, стакан 200 мл.
        var milk = new NutritionValues(60, 3, 3.2, 4.7);
        var portion = NutritionMath.ForQuantity(milk, 200);

        Assert.Equal(120, portion.Kcal, precision: 6);
        Assert.Equal(6, portion.Protein, precision: 6);
        Assert.Equal(6.4, portion.Fat, precision: 6);
        Assert.Equal(9.4, portion.Carbs, precision: 6);
    }

    [Fact]
    public void Zero_quantity_gives_zero_values()
    {
        var portion = NutritionMath.ForQuantity(ChickenPer100, 0);

        Assert.Equal(0, portion.Kcal, precision: 6);
        Assert.Equal(0, portion.Protein, precision: 6);
        Assert.Equal(0, portion.Fat, precision: 6);
        Assert.Equal(0, portion.Carbs, precision: 6);
    }

    [Fact]
    public void Negative_quantity_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => NutritionMath.ForQuantity(ChickenPer100, -10));
    }
}
