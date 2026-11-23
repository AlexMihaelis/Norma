using Norma.Api.Data.Entities;
using Norma.Api.Data.Seeding;

namespace Norma.Api.Tests.Seeding;

/// <summary>
/// Проверки стартового набора продуктов: без базы, только правила данных. Набор
/// попадает в общий каталог, поэтому опечатка в КБЖУ испортит цифры всем.
/// </summary>
public sealed class FoodSeedTests
{
    [Fact]
    public void Seed_has_enough_items_for_starter_catalog()
    {
        Assert.True(FoodSeed.Items.Count >= 50, "Стартовый набор слишком мал, чтобы поиск был полезен.");
    }

    [Fact]
    public void Names_are_unique_and_have_sane_length()
    {
        var names = FoodSeed.Items.Select(item => item.Name.ToLowerInvariant()).ToList();

        Assert.Equal(names.Count, names.Distinct().Count());
        Assert.All(FoodSeed.Items, item =>
        {
            Assert.InRange(item.Name.Length, 2, 100);
            Assert.False(string.IsNullOrWhiteSpace(item.Name));
        });
    }

    [Fact]
    public void Values_fit_catalog_rules()
    {
        foreach (var item in FoodSeed.Items)
        {
            Assert.InRange(item.Kcal, 0, 950);
            Assert.InRange(item.Protein, 0, 100);
            Assert.InRange(item.Fat, 0, 100);
            Assert.InRange(item.Carbs, 0, 100);
            Assert.True(
                item.Protein + item.Fat + item.Carbs <= 100.5,
                $"{item.Name}: сумма БЖУ больше 100 г на 100 г.");
        }
    }

    [Fact]
    public void Aliases_are_short_and_unique_inside_item()
    {
        foreach (var item in FoodSeed.Items)
        {
            var aliases = item.Aliases ?? [];

            Assert.Equal(aliases.Length, aliases.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.All(aliases, alias => Assert.InRange(alias.Length, 2, 100));
        }
    }

    [Fact]
    public void Dry_and_cooked_grains_are_separate_entries()
    {
        // Гречка сухая ~343 ккал, варёная ~110: перепутать - значит соврать втрое.
        var buckwheat = FoodSeed.Items.Where(item => item.Name.StartsWith("Гречка", StringComparison.Ordinal)).ToList();

        Assert.Equal(2, buckwheat.Count);
        Assert.Contains(buckwheat, item => item.State == FoodState.Raw);
        Assert.Contains(buckwheat, item => item.State == FoodState.Cooked);
    }

    [Fact]
    public void Liquids_use_milliliters()
    {
        var liquids = new[] { "Молоко 2,5%", "Кефир 1%", "Сливки 10%", "Сок яблочный" };

        foreach (var name in liquids)
        {
            var item = FoodSeed.Items.Single(candidate => candidate.Name == name);

            Assert.Equal(FoodUnit.Milliliter, item.Unit);
        }
    }
}