namespace Norma.Api.Data.Entities;

/// <summary>
/// Прием пищи - верхний уровень дневника: дата и тип. Что именно съедено, лежит
/// позициями в <see cref="MealItem"/>.
/// </summary>
/// <remarks>
/// Двухуровневая модель выбрана, чтобы на прием целиком можно было повесить время,
/// заметку или «повторить этот завтрак». Сейчас таких полей нет, но структура под
/// них готова, а позиции остаются атомарными записями.
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
