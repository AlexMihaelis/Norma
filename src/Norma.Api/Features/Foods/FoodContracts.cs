namespace Norma.Api.Features.Foods;

/// <summary>
/// Тело создания своего продукта. Обязательны название и КБЖУ; состояние и
/// единица по умолчанию Unknown и Gram. КБЖУ - на 100 г или 100 мл.
/// </summary>
public sealed record CreateFoodRequest(
    string? Name,
    IReadOnlyList<string>? Aliases,
    string? State,
    string? BaseUnit,
    double? KcalPer100,
    double? ProteinPer100,
    double? FatPer100,
    double? CarbsPer100);
