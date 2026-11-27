using Norma.Core.Nutrition;
using Norma.Core.Recipes;

namespace Norma.Core.Tests.Recipes;

/// <summary>
/// Расчет рецепта: состав складывается, а на 100 г считается делением. Главное
/// свойство - цифры на 100 г не зависят от того, готовят одну порцию или десять.
/// </summary>
public sealed class RecipeMathTests
{
    [Fact]
    public void Per100_of_one_ingredient_equals_its_catalog_values()
    {
        var chicken = new NutritionValues(165, 31, 3.6, 0);

        var totals = RecipeMath.Compute([new RecipeLine(chicken, 150)]);

        Assert.Equal(150, totals.Grams);
        Assert.Equal(165 * 1.5, totals.Total.Kcal, 10);
        Assert.Equal(165, totals.Per100.Kcal, 10);
        Assert.Equal(31, totals.Per100.Protein, 10);
    }

    [Fact]
    public void Per100_is_weighted_by_grams()
    {
        // 100 г по 100 ккал и 300 г по 300 ккал: в сумме 1000 ккал на 400 г = 250.
        var light = new NutritionValues(100, 10, 0, 0);
        var heavy = new NutritionValues(300, 0, 0, 0);

        var totals = RecipeMath.Compute([new RecipeLine(light, 100), new RecipeLine(heavy, 300)]);

        Assert.Equal(400, totals.Grams);
        Assert.Equal(1000, totals.Total.Kcal, 10);
        Assert.Equal(250, totals.Per100.Kcal, 10);
    }

    [Fact]
    public void Scaling_the_recipe_keeps_per100()
    {
        var rice = new NutritionValues(343, 7, 0.6, 78);
        var milk = new NutritionValues(60, 3, 3.2, 4.7);

        var one = RecipeMath.Compute([new RecipeLine(rice, 80), new RecipeLine(milk, 150)]);
        var ten = RecipeMath.Compute([new RecipeLine(rice, 800), new RecipeLine(milk, 1500)]);

        Assert.Equal(one.Per100.Kcal, ten.Per100.Kcal, 10);
        Assert.Equal(one.Per100.Protein, ten.Per100.Protein, 10);
        Assert.Equal(one.Per100.Fat, ten.Per100.Fat, 10);
        Assert.Equal(one.Per100.Carbs, ten.Per100.Carbs, 10);
        Assert.Equal(ten.Grams, one.Grams * 10, 10);
    }

    [Fact]
    public void Empty_recipe_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => RecipeMath.Compute([]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(double.NaN)]
    public void Non_positive_grams_are_rejected(double grams)
    {
        var line = new RecipeLine(new NutritionValues(100, 1, 1, 1), grams);

        Assert.Throws<ArgumentOutOfRangeException>(() => RecipeMath.Compute([line]));
    }
}
