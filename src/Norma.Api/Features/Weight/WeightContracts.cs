namespace Norma.Api.Features.Weight;

/// <summary>Один замер веса в ответе API.</summary>
public sealed record WeightResponse(Guid Id, DateOnly Date, double Kg);

/// <summary>Список замеров веса пользователя.</summary>
public sealed record WeightListResponse(IReadOnlyList<WeightResponse> Items);

/// <summary>Точка сглаженной кривой тренда.</summary>
public sealed record TrendPointResponse(DateOnly Date, double EmaKg);

/// <summary>
/// Тренд веса: сглаженная кривая, скорость и прогноз даты цели. Все числа
/// nullable, потому что замеров может ещё не быть. <see cref="TargetKg"/> — цель
/// из профиля, от неё считается дата достижения.
/// </summary>
public sealed record WeightTrendResponse(
    double? CurrentEmaKg,
    double? RateKgPerWeek,
    double? TargetKg,
    DateOnly? GoalDate,
    IReadOnlyList<TrendPointResponse> Points);
