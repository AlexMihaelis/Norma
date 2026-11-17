using Norma.Core.Bmi;
using Norma.Core.Energy;

namespace Norma.Api.Features.Weight;

/// <summary>Один замер веса в ответе API.</summary>
public sealed record WeightResponse(Guid Id, DateOnly Date, double Kg);

/// <summary>Список замеров веса пользователя.</summary>
public sealed record WeightListResponse(IReadOnlyList<WeightResponse> Items);

/// <summary>Точка сглаженной кривой тренда.</summary>
public sealed record TrendPointResponse(DateOnly Date, double EmaKg);

/// <summary>Норма веса для роста и возраста: границы в килограммах и в ИМТ.</summary>
public sealed record NormalWeightResponse(
    double MinKg,
    double MaxKg,
    double BmiMin,
    double BmiMax);

/// <summary>
/// Тренд веса: кривая, скорость, прогноз даты цели, ИМТ и норма веса.
/// Норма зависит от роста и возраста: 18-64 года - 18.5-25, 65+ - 22-27.
/// Пол входит в ответ для контекста, но на границы не влияет: пороги ВОЗ для
/// взрослых одинаковы. ИМТ считается по последнему замеру.
/// </summary>
public sealed record WeightTrendResponse(
    double? CurrentEmaKg,
    double? RateKgPerWeek,
    double? TargetKg,
    DateOnly? GoalDate,
    double? HeightCm,
    Sex? Sex,
    int? Age,
    WeightAgeBand AgeBand,
    double? Bmi,
    BmiCategory? BmiCategory,
    double? ObeseFromKg,
    NormalWeightResponse? NormalWeight,
    IReadOnlyList<TrendPointResponse> Points);
