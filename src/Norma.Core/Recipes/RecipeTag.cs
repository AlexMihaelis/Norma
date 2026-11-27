namespace Norma.Core.Recipes;

/// <summary>
/// Метки рецепта, которые ставит человек: прием пищи, тип блюда и способ
/// приготовления. Автоматически такое не выводится - «в мультиварке» по составу
/// не угадать.
/// </summary>
public enum RecipeTag
{
    Breakfast,
    Lunch,
    Dinner,
    Snack,
    Salad,
    Soup,
    Baking,
    Appetizer,
    AirFryer,
    SlowCooker,
    Oven,
    Pan,
}

/// <summary>
/// Метки, которые считаются по составу: основной продукт и баланс макросов.
/// Хранить их в базе незачем - они всегда пересчитываются из ингредиентов.
/// </summary>
public enum RecipeAutoTag
{
    Chicken,
    Turkey,
    Pork,
    Beef,
    Eggs,
    Fish,
    HighProtein,
    LowFat,
}
