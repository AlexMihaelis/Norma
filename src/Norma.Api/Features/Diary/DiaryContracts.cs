using Norma.Api.Data.Entities;

namespace Norma.Api.Features.Diary;

/// <summary>Чем является позиция дневника: продуктом каталога или рецептом.</summary>
public enum DiaryItemKind
{
    Food,
    Recipe,
}

/// <summary>Позиция дневника: продукт или рецепт, количество и посчитанные КБЖУ.</summary>
public sealed record DiaryEntryResponse(
    Guid Id,
    DiaryItemKind Kind,
    Guid? FoodId,
    Guid? RecipeId,
    string FoodName,
    double Quantity,
    FoodUnit Unit,
    int Kcal,
    double Protein,
    double Fat,
    double Carbs);

/// <summary>Суммы по приему пищи или по дню.</summary>
public sealed record DiaryTotalsResponse(int Kcal, double Protein, double Fat, double Carbs);

/// <summary>Прием пищи за день; пустой прием отдается с пустым списком позиций.</summary>
public sealed record DiaryMealResponse(
    MealKind Kind,
    IReadOnlyList<DiaryEntryResponse> Items,
    DiaryTotalsResponse Totals);

/// <summary>День дневника: шесть приемов в хронологическом порядке и итоги дня.</summary>
public sealed record DiaryDayResponse(
    DateOnly Date,
    IReadOnlyList<DiaryMealResponse> Meals,
    DiaryTotalsResponse Totals);

/// <summary>
/// Тело записи позиции. Нужно ровно одно из полей <c>foodId</c> и <c>recipeId</c>:
/// продукт из каталога или рецепт пользователя. Дата по умолчанию - сегодня,
/// единица - базовая у продукта, у рецепта всегда граммы.
/// </summary>
public sealed record AddDiaryEntryRequest(
    string? Meal,
    Guid? FoodId,
    Guid? RecipeId,
    double? Quantity,
    string? Unit,
    DateOnly? Date);

/// <summary>Тело повтора: пустое означает «вчера в сегодня, все приемы».</summary>
public sealed record RepeatDiaryRequest(DateOnly? FromDate, DateOnly? Date, string? Meal);

/// <summary>Сколько позиций скопировано и как выглядит целевой день после копирования.</summary>
public sealed record RepeatDiaryResponse(
    DateOnly Date,
    int CopiedCount,
    string? Hint,
    IReadOnlyList<DiaryMealResponse> Meals,
    DiaryTotalsResponse Totals);
