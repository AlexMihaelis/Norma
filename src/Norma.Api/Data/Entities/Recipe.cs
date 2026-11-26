namespace Norma.Api.Data.Entities;

/// <summary>
/// Рецепт пользователя: название, заметка и состав из продуктов с граммовками.
/// Пищевая ценность не хранится - она считается из состава при чтении.
/// </summary>
/// <remarks>
/// Рецепт принадлежит одному пользователю (в отличие от продукта, который лежит
/// в общем каталоге): чужие рецепты не видны ни в списке, ни по идентификатору.
/// </remarks>
public sealed class Recipe
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Способ приготовления по шагам, необязательно.</summary>
    public string? Instructions { get; set; }

    /// <summary>Фото готового блюда: тип и байты, как у аватара.</summary>
    public string? PhotoContentType { get; set; }

    public byte[]? PhotoBytes { get; set; }

    /// <summary>Отметка «избранный рецепт»: рецепт принадлежит пользователю, флаг рядом.</summary>
    public bool IsFavorite { get; set; }

    /// <summary>
    /// Свои КБЖУ на 100 г, если человек их ввел. Пусто - цифры считаются из
    /// состава, как обычно.
    /// </summary>
    public double? ManualKcalPer100g { get; set; }

    public double? ManualProteinPer100g { get; set; }

    public double? ManualFatPer100g { get; set; }

    public double? ManualCarbsPer100g { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public List<RecipeIngredient> Ingredients { get; set; } = [];

    public List<RecipeTagLink> Tags { get; set; } = [];

    public User? User { get; set; }
}

/// <summary>
/// Строка состава: продукт и его вес. КБЖУ строки считается из продукта.
/// </summary>
/// <remarks>
/// Вес задается в базовой единице продукта: у обычных продуктов это граммы, у
/// жидкостей - миллилитры. Плотности в базе нет, поэтому числа складываются как
/// есть, а перевод «миллилитры в граммы» остается задачей на будущее.
/// </remarks>
public sealed class RecipeIngredient
{
    public Guid Id { get; set; }

    public Guid RecipeId { get; set; }

    public Guid FoodId { get; set; }

    public double Grams { get; set; }

    public Recipe? Recipe { get; set; }

    public Food? Food { get; set; }
}
