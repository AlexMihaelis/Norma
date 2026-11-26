namespace Norma.Api.Features.Recipes;

/// <summary>Строка состава в запросе: продукт и его вес.</summary>
public sealed record RecipeIngredientRequest(Guid? FoodId, double? Grams);

/// <summary>
/// Тело создания и полной замены рецепта. Замена полная, как у профиля: чего нет
/// в теле, того не будет и в рецепте (включая фото).
/// </summary>
public sealed record RecipeRequest(
    string? Name,
    string? Instructions,
    IReadOnlyList<string>? Tags,
    string? PhotoDataUrl,
    IReadOnlyList<RecipeIngredientRequest>? Ingredients);

/// <summary>
/// Строка состава в ответе: продукт, вес, калории строки и ценности продукта на
/// 100 единиц - по ним форма рецепта пересчитывает итог без второго запроса.
/// </summary>
public sealed record RecipeIngredientResponse(
    Guid FoodId,
    string Name,
    double Grams,
    double Kcal,
    double KcalPer100g,
    double ProteinPer100g,
    double FatPer100g,
    double CarbsPer100g);

/// <summary>Рецепт в списке: без состава, но с цифрами на 100 г, метками и фото.</summary>
public sealed record RecipeSummary(
    Guid Id,
    string Name,
    double Grams,
    double KcalPer100g,
    double ProteinPer100g,
    double FatPer100g,
    double CarbsPer100g,
    double Kcal,
    int IngredientCount,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> AutoTags,
    bool IsFavorite,
    string? PhotoDataUrl,
    DateTimeOffset CreatedAt);

/// <summary>Рецепт целиком: состав, инструкция и посчитанные из состава цифры.</summary>
public sealed record RecipeResponse(
    Guid Id,
    string Name,
    string? Instructions,
    double Grams,
    double KcalPer100g,
    double ProteinPer100g,
    double FatPer100g,
    double CarbsPer100g,
    double Kcal,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> AutoTags,
    bool IsFavorite,
    string? PhotoDataUrl,
    IReadOnlyList<RecipeIngredientResponse> Ingredients,
    DateTimeOffset CreatedAt);

/// <summary>Список рецептов пользователя.</summary>
public sealed record RecipeListResponse(IReadOnlyList<RecipeSummary> Items);

/// <summary>Метка в справочнике фильтров: код, подпись, группа и признак «сама».</summary>
public sealed record RecipeTagResponse(string Code, string Title, string Group, bool Auto);

/// <summary>Справочник меток для панели фильтров.</summary>
public sealed record RecipeTagListResponse(IReadOnlyList<RecipeTagResponse> Items);

/// <summary>Тело переключателя избранного.</summary>
public sealed record FavoriteRecipeRequest(bool? IsFavorite);
