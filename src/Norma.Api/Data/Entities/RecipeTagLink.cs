namespace Norma.Api.Data.Entities;

/// <summary>
/// Метка рецепта, которую поставил человек: прием пищи, тип блюда или способ
/// приготовления. Хранится отдельными строками, чтобы фильтр по метке был
/// обычным JOIN, а не поиском по строке с разделителями.
/// </summary>
public sealed class RecipeTagLink
{
    public Guid Id { get; set; }

    public Guid RecipeId { get; set; }

    /// <summary>Код метки из <c>Norma.Core.Recipes.RecipeTag</c> строкой.</summary>
    public string Tag { get; set; } = string.Empty;

    public Recipe? Recipe { get; set; }
}
