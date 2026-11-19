namespace Norma.Core.Nutrition;

/// <summary>Пищевая ценность на 100 базовых единиц продукта (граммов или миллилитров).</summary>
public readonly record struct NutritionValues(double Kcal, double Protein, double Fat, double Carbs);

/// <summary>
/// Пересчет пищевой ценности продукта на съеденное количество. Чистая функция:
/// количество и значения приходят параметрами, база и текущее время не нужны.
/// </summary>
/// <remarks>
/// Формула одна для граммов и миллилитров: значение × количество / 100.
/// </remarks>
public static class NutritionMath
{
    /// <summary>Пищевая ценность порции количеством <paramref name="quantity"/>.</summary>
    /// <param name="per100">Значения продукта на 100 г или 100 мл.</param>
    /// <param name="quantity">Съеденное количество в тех же единицах.</param>
    public static NutritionValues ForQuantity(NutritionValues per100, double quantity)
    {
        if (quantity < 0 || double.IsNaN(quantity))
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity), quantity, "Количество не может быть отрицательным.");
        }

        var factor = quantity / 100.0;

        return new NutritionValues(
            per100.Kcal * factor,
            per100.Protein * factor,
            per100.Fat * factor,
            per100.Carbs * factor);
    }
}
