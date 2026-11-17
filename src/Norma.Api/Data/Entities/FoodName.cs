namespace Norma.Api.Data.Entities;

/// <summary>
/// Название продукта на конкретной локали. Русское название - результат
/// кураторства, а не дословный перевод записи USDA, поэтому хранится отдельно от
/// исходных данных и может иметь несколько вариантов (основное одно).
/// </summary>
public sealed class FoodName
{
    public Guid Id { get; set; }

    public Guid FoodId { get; set; }

    /// <summary>Язык названия, например <c>ru</c>.</summary>
    public string Locale { get; set; } = "ru";

    public string Name { get; set; } = string.Empty;

    /// <summary>Название для показа; у продукта должно быть ровно одно основное.</summary>
    public bool IsPrimary { get; set; }

    public Food? Food { get; set; }
}
