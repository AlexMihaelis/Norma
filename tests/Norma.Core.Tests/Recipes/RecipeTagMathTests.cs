using Norma.Core.Nutrition;
using Norma.Core.Recipes;

namespace Norma.Core.Tests.Recipes;

/// <summary>
/// Автоматические метки: основной продукт ищется по основе названия, а «много
/// белка» и «мало жиров» - по доле калорий из макроса.
/// </summary>
public sealed class RecipeTagMathTests
{
    [Theory]
    [InlineData("Куриная грудка", RecipeAutoTag.Chicken)]
    [InlineData("Филе индейки", RecipeAutoTag.Turkey)]
    [InlineData("Свинина, шея", RecipeAutoTag.Pork)]
    [InlineData("Говядина, лопатка", RecipeAutoTag.Beef)]
    [InlineData("Яйцо куриное", RecipeAutoTag.Eggs)]
    [InlineData("Треска, филе", RecipeAutoTag.Fish)]
    public void Product_is_recognized_by_name_stem(string product, RecipeAutoTag expected)
    {
        var tags = RecipeTagMath.Detect([product], new NutritionValues(100, 5, 5, 5));

        Assert.Contains(expected, tags);
    }

    [Fact]
    public void Protein_rich_recipe_gets_high_protein_tag()
    {
        // 25 г белка на 100 г = 100 ккал из 150: около 67 % калорий.
        var tags = RecipeTagMath.Detect(["Куриная грудка"], new NutritionValues(150, 25, 5, 0));

        Assert.Contains(RecipeAutoTag.HighProtein, tags);
        Assert.DoesNotContain(RecipeAutoTag.LowFat, tags);
    }

    [Fact]
    public void Low_fat_recipe_gets_low_fat_tag()
    {
        // 2 г жира = 18 ккал из 100: 18 % калорий.
        var tags = RecipeTagMath.Detect(["Рис"], new NutritionValues(100, 5, 2, 15));

        Assert.Contains(RecipeAutoTag.LowFat, tags);
        Assert.DoesNotContain(RecipeAutoTag.HighProtein, tags);
    }

    [Fact]
    public void Zero_calorie_recipe_has_no_macro_tags()
    {
        var tags = RecipeTagMath.Detect(["Вода"], new NutritionValues(0, 0, 0, 0));

        Assert.Empty(tags);
    }

    [Fact]
    public void Names_are_matched_case_insensitively()
    {
        var tags = RecipeTagMath.Detect(["КУРИЦА отварная"], new NutritionValues(200, 20, 8, 0));

        Assert.Contains(RecipeAutoTag.Chicken, tags);
    }
}
