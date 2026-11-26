using Norma.Core.Recipes;

namespace Norma.Api.Features.Recipes;

/// <summary>
/// Справочник меток для панели фильтров: что человек ставит сам и что система
/// считает по составу. Подписи живут здесь, в ядре только коды.
/// </summary>
internal static class RecipeTagCatalog
{
    private const string MealGroup = "Приемы пищи";
    private const string DishGroup = "Тип блюда";
    private const string CookingGroup = "Способ приготовления";
    private const string AutoGroup = "Подбирается само";

    private static readonly (RecipeTag Tag, string Title, string Group)[] Manual =
    [
        (RecipeTag.Breakfast, "завтрак", MealGroup),
        (RecipeTag.Lunch, "обед", MealGroup),
        (RecipeTag.Dinner, "ужин", MealGroup),
        (RecipeTag.Snack, "перекус", MealGroup),
        (RecipeTag.Salad, "салат", DishGroup),
        (RecipeTag.Soup, "суп", DishGroup),
        (RecipeTag.Baking, "выпечка", DishGroup),
        (RecipeTag.AirFryer, "аэрогриль", CookingGroup),
        (RecipeTag.SlowCooker, "мультиварка", CookingGroup),
        (RecipeTag.Oven, "духовка", CookingGroup),
        (RecipeTag.Pan, "сковорода", CookingGroup),
    ];

    private static readonly (RecipeAutoTag Tag, string Title)[] Auto =
    [
        (RecipeAutoTag.Chicken, "курица"),
        (RecipeAutoTag.Turkey, "индейка"),
        (RecipeAutoTag.Pork, "свинина"),
        (RecipeAutoTag.Beef, "говядина"),
        (RecipeAutoTag.Eggs, "яйца"),
        (RecipeAutoTag.Fish, "рыба"),
        (RecipeAutoTag.HighProtein, "много белка"),
        (RecipeAutoTag.LowFat, "мало жиров"),
    ];

    public static IReadOnlyList<RecipeTagResponse> Build() =>
        Manual
            .Select(tag => new RecipeTagResponse(tag.Tag.ToString(), tag.Title, tag.Group))
            .Concat(Auto.Select(tag => new RecipeTagResponse(tag.Tag.ToString(), tag.Title, AutoGroup)))
            .ToList();

    /// <summary>Код метки, которую человек ставит сам; <c>null</c>, если код неизвестен.</summary>
    public static RecipeTag? ParseManual(string? code) =>
        Enum.TryParse<RecipeTag>(code, ignoreCase: true, out var tag) && Enum.IsDefined(tag)
            ? tag
            : null;

    /// <summary>Код автоматической метки для фильтра; <c>null</c>, если код неизвестен.</summary>
    public static RecipeAutoTag? ParseAuto(string? code) =>
        Enum.TryParse<RecipeAutoTag>(code, ignoreCase: true, out var tag) && Enum.IsDefined(tag)
            ? tag
            : null;
}
