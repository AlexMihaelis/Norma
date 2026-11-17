namespace Norma.Api.Data.Entities;

/// <summary>
/// Синоним для поиска: «гречка» должно находить «Крупу гречневую». Отдельная
/// таблица, чтобы поиск не зависел от того, как называется продукт официально.
/// </summary>
public sealed class FoodAlias
{
    public Guid Id { get; set; }

    public Guid FoodId { get; set; }

    public string Alias { get; set; } = string.Empty;

    public Food? Food { get; set; }
}
