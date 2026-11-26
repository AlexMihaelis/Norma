using Norma.Core.Nutrition;

namespace Norma.Core.Recipes;

/// <summary>
/// Автоматические метки рецепта: по каким продуктам он состоит и как в нем
/// распределены калории. Чистая функция - на входе названия продуктов и цифры
/// на 100 г, на выходе набор меток.
/// </summary>
/// <remarks>
/// Названия сравниваются по основам слов («куриц», «индейк»), а не целиком:
/// в каталоге встречаются «куриная грудка», «курица отварная», «филе индейки».
/// </remarks>
public static class RecipeTagMath
{
    /// <summary>Доля калорий из белка, с которой рецепт считается белковым.</summary>
    public const double HighProteinShare = 0.30;

    /// <summary>Доля калорий из жира, ниже которой рецепт считается нежирным.</summary>
    public const double LowFatShare = 0.20;

    private static readonly (RecipeAutoTag Tag, string[] Stems)[] ProteinTags =
    [
        (RecipeAutoTag.Chicken, ["куриц", "курин", "цыплен"]),
        (RecipeAutoTag.Turkey, ["индейк", "индюш"]),
        (RecipeAutoTag.Pork, ["свин"]),
        (RecipeAutoTag.Beef, ["говядин", "телятин"]),
        (RecipeAutoTag.Eggs, ["яйц", "яичн"]),
        (RecipeAutoTag.Fish, ["рыб", "лосос", "треск", "минтай", "тунец", "сельд", "скумбри"]),
    ];

    /// <summary>Считает метки по составу рецепта.</summary>
    /// <param name="ingredientNames">Названия продуктов из состава.</param>
    /// <param name="per100">Пищевая ценность рецепта на 100 г.</param>
    public static IReadOnlySet<RecipeAutoTag> Detect(
        IEnumerable<string> ingredientNames,
        NutritionValues per100)
    {
        ArgumentNullException.ThrowIfNull(ingredientNames);

        var names = ingredientNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.ToLowerInvariant())
            .ToList();

        var tags = new HashSet<RecipeAutoTag>();

        foreach (var (tag, stems) in ProteinTags)
        {
            if (names.Any(name => stems.Any(stem => name.Contains(stem, StringComparison.Ordinal))))
            {
                tags.Add(tag);
            }
        }

        var kcal = per100.Kcal;

        if (kcal > 0)
        {
            // Белок и углеводы дают по 4 ккал на грамм, жир - 9.
            if (per100.Protein * 4 / kcal >= HighProteinShare)
            {
                tags.Add(RecipeAutoTag.HighProtein);
            }

            if (per100.Fat * 9 / kcal <= LowFatShare)
            {
                tags.Add(RecipeAutoTag.LowFat);
            }
        }

        return tags;
    }
}
