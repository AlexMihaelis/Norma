using Norma.Core.Nutrition;

namespace Norma.Core.Recipes;

/// <summary>
/// Строка рецепта: пищевая ценность продукта на 100 единиц и вес этой строки.
/// </summary>
/// <param name="Per100">Значения продукта на 100 г (или 100 мл) из каталога.</param>
/// <param name="Grams">Сколько продукта идет в рецепт.</param>
public readonly record struct RecipeLine(NutritionValues Per100, double Grams);

/// <summary>Итог рецепта: общий вес, ценность всего состава и на 100 г.</summary>
public readonly record struct RecipeTotals(double Grams, NutritionValues Total, NutritionValues Per100);

/// <summary>
/// Пересчет рецепта из состава. Чистая функция: продукты и граммовки приходят
/// параметрами, база и время не нужны.
/// </summary>
/// <remarks>
/// КБЖУ рецепта не хранится в базе, а считается при каждом чтении - так же, как
/// пищевая ценность позиции дневника считается из продукта. Правка каталога или
/// состава сразу меняет цифры рецепта, расхождений между копиями не бывает.
/// </remarks>
public static class RecipeMath
{
    /// <summary>Считает рецепт по строкам состава.</summary>
    /// <param name="lines">Продукты с граммовками; хотя бы одна строка.</param>
    /// <exception cref="ArgumentException">Состав пуст.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Вес строки не положительный.</exception>
    public static RecipeTotals Compute(IEnumerable<RecipeLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var grams = 0.0;
        var total = new NutritionValues(0, 0, 0, 0);
        var count = 0;

        foreach (var line in lines)
        {
            if (line.Grams <= 0 || double.IsNaN(line.Grams) || double.IsInfinity(line.Grams))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(lines),
                    line.Grams,
                    "Вес строки рецепта должен быть положительным числом.");
            }

            var portion = NutritionMath.ForQuantity(line.Per100, line.Grams);

            grams += line.Grams;
            total = new NutritionValues(
                total.Kcal + portion.Kcal,
                total.Protein + portion.Protein,
                total.Fat + portion.Fat,
                total.Carbs + portion.Carbs);

            count++;
        }

        if (count == 0)
        {
            throw new ArgumentException("Рецепт без ингредиентов не считается.", nameof(lines));
        }

        // На 100 г: делим итог на вес и умножаем на сто. Округления здесь нет -
        // его делает интерфейс, чтобы сорок граммов и килограмм одного и того же
        // блюда показывали одну цифру.
        var factor = 100.0 / grams;

        return new RecipeTotals(
            grams,
            total,
            new NutritionValues(
                total.Kcal * factor,
                total.Protein * factor,
                total.Fat * factor,
                total.Carbs * factor));
    }
}
