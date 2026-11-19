namespace Norma.Api.Data.Entities;

/// <summary>
/// Позиция приема пищи: продукт и съеденное количество. КБЖУ здесь не хранится -
/// оно считается из продукта при чтении.
/// </summary>
/// <remarks>
/// Решение «пересчет, а не снимок»: правка каталога меняет историю, поэтому
/// продукт, который уже есть в дневнике, удалять нельзя.
/// </remarks>
public sealed class MealItem
{
    public Guid Id { get; set; }

    public Guid MealId { get; set; }

    public Guid FoodId { get; set; }

    /// <summary>Количество в <see cref="Unit"/> (граммы или миллилитры).</summary>
    public double Quantity { get; set; }

    /// <summary>
    /// Единица на момент записи. Совпадает с базовой единицей продукта: без
    /// плотности перевести граммы в миллилитры нельзя.
    /// </summary>
    public FoodUnit Unit { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Meal? Meal { get; set; }

    public Food? Food { get; set; }
}
