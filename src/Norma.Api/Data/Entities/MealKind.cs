namespace Norma.Api.Data.Entities;

/// <summary>
/// Тип приема пищи. Все типы необязательны: день может быть заполнен частично,
/// и пустые приемы в ответе просто остаются пустыми.
/// </summary>
/// <remarks>
/// Порядок значений хронологический: по нему UI строит день без справочника.
/// </remarks>
public enum MealKind
{
    Breakfast,
    MorningSnack,
    Lunch,
    AfternoonSnack,
    Dinner,
    EveningSnack,
}
