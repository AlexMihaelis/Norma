using Norma.Api.Data.Entities;

namespace Norma.Api.Features.Diary;

/// <summary>
/// Разбор строковых значений дневника в enum-ы: одна валидация на запись позиции и
/// на повтор дня.
/// </summary>
internal static class DiaryParsing
{
    /// <summary>Прием пищи обязателен: пустое значение - ошибка.</summary>
    public static MealKind? ParseRequiredMeal(string? raw, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            errors["meal"] = [$"Укажите прием пищи: {string.Join(", ", Enum.GetNames<MealKind>())}."];
            return null;
        }

        return ParseMeal(raw, errors);
    }

    /// <summary>Прием пищи необязателен: пустое значение означает «все приемы».</summary>
    public static MealKind? ParseOptionalMeal(string? raw, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return ParseMeal(raw, errors);
    }

    /// <summary>Единица необязательна: по умолчанию берется базовая у продукта.</summary>
    public static FoodUnit? ParseUnit(string? raw, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (!Enum.TryParse<FoodUnit>(raw, ignoreCase: true, out var unit) || !Enum.IsDefined(unit))
        {
            errors["unit"] = [$"Недопустимая единица: ожидается {string.Join(", ", Enum.GetNames<FoodUnit>())}."];
            return null;
        }

        return unit;
    }

    private static MealKind? ParseMeal(string raw, Dictionary<string, string[]> errors)
    {
        if (!Enum.TryParse<MealKind>(raw, ignoreCase: true, out var kind) || !Enum.IsDefined(kind))
        {
            errors["meal"] = [$"Недопустимый прием пищи: ожидается {string.Join(", ", Enum.GetNames<MealKind>())}."];
            return null;
        }

        return kind;
    }
}
