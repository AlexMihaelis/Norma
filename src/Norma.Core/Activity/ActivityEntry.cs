namespace Norma.Core.Activity;

/// <summary>
/// Виды активности, для которых есть MET-значение. Список намеренно короткий:
/// лучше десять честных значений, чем сто приблизительных.
/// </summary>
public enum ActivityKind
{
    Walking,
    Running,
    Cycling,
    Swimming,
    StrengthTraining,
    Yoga,
    Dancing,
    Hiking,
    RowingMachine,
    DownhillSkiing,
}

/// <summary>Запись активности за день: вид и длительность.</summary>
public sealed record ActivityEntry(ActivityKind Kind, int Minutes);
