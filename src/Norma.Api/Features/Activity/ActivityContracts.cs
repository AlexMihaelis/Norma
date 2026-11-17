using Norma.Core.Activity;

namespace Norma.Api.Features.Activity;

/// <summary>Сохраненная активность: вид, минуты и калории-снимок.</summary>
public sealed record ActivityResponse(
    Guid Id,
    DateOnly Date,
    ActivityKind Kind,
    int Minutes,
    int Kcal);

/// <summary>Активность за день: записи и суммарный расход.</summary>
public sealed record ActivityDayResponse(
    DateOnly Date,
    IReadOnlyList<ActivityResponse> Items,
    int TotalKcal);

/// <summary>Тело записи активности: вид, минуты и дата (по умолчанию сегодня).</summary>
public sealed record AddActivityRequest(string? Kind, int? Minutes, DateOnly? Date);
