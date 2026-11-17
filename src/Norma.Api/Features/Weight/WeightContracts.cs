using Norma.Core.Bmi;

namespace Norma.Api.Features.Weight;

/// <summary>Один замер веса в ответе API.</summary>
public sealed record WeightResponse(Guid Id, DateOnly Date, double Kg);

/// <summary>Список замеров веса пользователя.</summary>
public sealed record WeightListResponse(IReadOnlyList<WeightResponse> Items);

/// <summary>Точка сглаженной кривой тренда.</summary>
public sealed record TrendPointResponse(DateOnly Date, double EmaKg);

/// <summary>
/// Тренд веса: сглаженная кривая, скорость и прогноз даты цели. Все числа
/// nullable, потому что замеров может ещё не быть. <c>TargetKg</c> — цель из
/// профиля, от неё считается дата достижения. ИМТ и границы весовых зон
/// считаются по последнему замеру и росту из профиля: без роста или веса они
/// null, а <c>HealthyWeightMinKg</c>, <c>HealthyWeightMaxKg</c> и
/// <c>ObeseFromKg</c> — это те же 18.5 / 25 / 30 ИМТ, переведенные в килограммы.
/// </summary>
public sealed record WeightTrendResponse(
    double? CurrentEmaKg,
    double? RateKgPerWeek,
    double? TargetKg,
    DateOnly? GoalDate,
    double? HeightCm,
    double? Bmi,
    BmiCategory? BmiCategory,
    double? HealthyWeightMinKg,
    double? HealthyWeightMaxKg,
    double? ObeseFromKg,
    IReadOnlyList<TrendPointResponse> Points);
