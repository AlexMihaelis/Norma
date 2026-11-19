namespace Norma.Api.Data.Entities;

/// <summary>
/// Прием пищи - верхний уровень дневника: дата и тип. Что именно съедено, лежит
/// позициями в <see cref="MealItem"/>.
/// </summary>
/// <remarks>
/// Двухуровневая модель: на прием целиком можно повесить время, заметку или
/// повтор, не трогая позиции.
/// </remarks>
public sealed class Meal
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public DateOnly Date { get; set; }

    public MealKind Kind { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public User? User { get; set; }

    public List<MealItem> Items { get; set; } = [];
}
