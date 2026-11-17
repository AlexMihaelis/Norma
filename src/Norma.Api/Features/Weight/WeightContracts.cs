using Norma.Core.Bmi;

namespace Norma.Api.Features.Weight;

/// <summary>Один замер веса в ответе API.</summary>
public sealed record WeightResponse(Guid Id, DateOnly Date, double Kg);

/// <summary>Список замеров веса пользователя.</summary>
public sealed record WeightListResponse(IReadOnlyList<WeightResponse> Items);

/// <summary>Точка сглаженной кривой тренда.</summary>
public sealed record TrendPointResponse(DateOnly Date, double EmaKg);

/// <summary>
/// Тренд веса: кривая, скорость, прогноз даты цели, ИМТ и границы весовых зон.
/// ИМТ считается по последнему замеру и росту; границы зон - те же 18.5 / 25 / 30,
/// переведенные в килограммы.
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
