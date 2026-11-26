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

/// <summary>Строка состава в ответе: продукт, вес и калории этой строки.</summary>
public sealed record RecipeIngredientResponse(Guid FoodId, string Name, double Grams, double Kcal);

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

/// <summary>Метка в справочнике фильтров: код, подпись и группа.</summary>
public sealed record RecipeTagResponse(string Code, string Title, string Group);

/// <summary>Справочник меток для панели фильтров.</summary>
public sealed record RecipeTagListResponse(IReadOnlyList<RecipeTagResponse> Items);

/// <summary>Тело переключателя избранного.</summary>
public sealed record FavoriteRecipeRequest(bool? IsFavorite);
