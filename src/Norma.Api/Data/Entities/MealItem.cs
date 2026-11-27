namespace Norma.Api.Data.Entities;

/// <summary>
/// Позиция приема пищи: продукт или рецепт и съеденное количество. КБЖУ здесь не
/// хранится - оно считается из продукта или состава рецепта при чтении.
/// </summary>
/// <remarks>
/// Решение «пересчет, а не снимок»: правка каталога или состава меняет историю,
/// поэтому продукт и рецепт, которые уже есть в дневнике, удалять нельзя.
/// Ровно одно из полей <see cref="FoodId"/> и <see cref="RecipeId"/> заполнено.
/// </remarks>
public sealed class MealItem
{
    public Guid Id { get; set; }

    public Guid MealId { get; set; }

    /// <summary>Продукт из каталога; <c>null</c>, если позиция - рецепт.</summary>
    public Guid? FoodId { get; set; }

    /// <summary>Рецепт пользователя; <c>null</c>, если позиция - продукт.</summary>
    public Guid? RecipeId { get; set; }

    /// <summary>Количество в <see cref="Unit"/> (граммы или миллилитры).</summary>
    public double Quantity { get; set; }

    /// <summary>
    /// Единица на момент записи. Совпадает с базовой единицей продукта: без
    /// плотности перевести граммы в миллилитры нельзя. У рецепта всегда граммы.
    /// </summary>
    public FoodUnit Unit { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Meal? Meal { get; set; }

    public Food? Food { get; set; }

    public Recipe? Recipe { get; set; }
}
