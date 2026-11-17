using Norma.Core.Activity;

namespace Norma.Api.Data.Entities;

/// <summary>
/// Одна запись активности: вид, длительность и калории, посчитанные на момент
/// записи. Калории хранятся снимком, чтобы история не менялась от новых замеров веса.
/// </summary>
public sealed class ActivityRecord
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public DateOnly Date { get; set; }

    public ActivityKind Kind { get; set; }

    public int Minutes { get; set; }

    public int Kcal { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public User? User { get; set; }
}
