namespace Norma.Api.Features.Recipes;

/// <summary>Строка состава в запросе: продукт и его вес.</summary>
public sealed record RecipeIngredientRequest(Guid? FoodId, double? Grams);

/// <summary>
/// Тело создания и полной замены рецепта. Замена полная, как у профиля: чего нет
/// в теле, того не будет и в рецепте.
/// </summary>
public sealed record RecipeRequest(
    string? Name,
    string? Note,
    IReadOnlyList<RecipeIngredientRequest>? Ingredients);

/// <summary>Строка состава в ответе: продукт, вес и калории этой строки.</summary>
public sealed record RecipeIngredientResponse(Guid FoodId, string Name, double Grams, double Kcal);

/// <summary>Рецепт в списке: без состава, но с цифрами на 100 г и всего.</summary>
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
    DateTimeOffset CreatedAt);

/// <summary>Рецепт целиком: состав и посчитанные из него цифры.</summary>
public sealed record RecipeResponse(
    Guid Id,
    string Name,
    string? Note,
    double Grams,
    double KcalPer100g,
    double ProteinPer100g,
    double FatPer100g,
    double CarbsPer100g,
    double Kcal,
    IReadOnlyList<RecipeIngredientResponse> Ingredients,
    DateTimeOffset CreatedAt);

/// <summary>Список рецептов пользователя.</summary>
public sealed record RecipeListResponse(IReadOnlyList<RecipeSummary> Items);
