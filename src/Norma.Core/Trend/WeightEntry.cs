namespace Norma.Core.Trend;

/// <summary>Один замер веса: дата и килограммы. Записей за день может быть несколько.</summary>
public sealed record WeightEntry(DateOnly Date, double Kg);

/// <summary>Точка сглаженной кривой: та же дата и значение EMA.</summary>
public sealed record TrendPoint(DateOnly Date, double EmaKg);

/// <summary>Результат анализа динамики веса.</summary>
/// <param name="Points">Сглаженная кривая по всем замерам.</param>
/// <param name="CurrentEmaKg">Текущий тренд (последнее значение EMA), кг.</param>
/// <param name="RateKgPerWeek">Скорость изменения тренда, кг/нед.</param>
/// <param name="GoalDate">Прогноз даты цели; <c>null</c> — цель недостижима текущим темпом.</param>
public sealed record WeightTrend(
    IReadOnlyList<TrendPoint> Points,
    double CurrentEmaKg,
    double RateKgPerWeek,
    DateOnly? GoalDate);
