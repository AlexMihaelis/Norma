using Norma.Api.Data.Entities;

namespace Norma.Api.Features.BodyMeasurements;

/// <summary>Тело записи обхвата: вид, сантиметры и дата (по умолчанию сегодня).</summary>
public sealed record BodyMeasurementRequest(string? Kind, double? Cm, DateOnly? Date);

/// <summary>Ответ на запись: что сохранили.</summary>
public sealed record BodyMeasurementResponse(Guid Id, DateOnly Date, BodyMeasurementKind Kind, double Cm);

/// <summary>
/// Последний замер вида плюс предыдущий: из пары считается изменение для UI.
/// </summary>
public sealed record BodyMeasurementLatestResponse(
    BodyMeasurementKind Kind,
    DateOnly Date,
    double Cm,
    DateOnly? PreviousDate,
    double? PreviousCm,
    double? DeltaCm);