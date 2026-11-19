namespace Norma.Api.Features.Stats;

/// <summary>Один день периода: съеденное, активность и цель на этот день.</summary>
public sealed record StatsDayResponse(
    DateOnly Date,
    int Kcal,
    double Protein,
    double Fat,
    double Carbs,
    int Entries,
    int ActivityKcal,
    int? TargetKcal,
    int? DiffKcal);

/// <summary>Средние по дням с записями: пустые дни в среднее не входят.</summary>
public sealed record StatsAveragesResponse(
    int DaysWithEntries,
    double Kcal,
    double Protein,
    double Fat,
    double Carbs,
    int ActivityKcal,
    int? TargetKcal);

/// <summary>Отклонение среднего факта от средней цели.</summary>
public sealed record StatsDeviationResponse(int TargetKcal, int DiffKcal, double DiffPercent);

/// <summary>
/// Статистика за период. `TargetHint` объясняет, почему цели нет или она есть не
/// для всех дней; `TargetNote` - постоянная оговорка о способе расчёта.
/// </summary>
public sealed record StatsResponse(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<StatsDayResponse> Days,
    StatsAveragesResponse Averages,
    StatsDeviationResponse? Deviation,
    string? TargetHint,
    string TargetNote);

/// <summary>Тексты статистики, которые обязан увидеть пользователь.</summary>
public static class StatsText
{
    /// <summary>
    /// Профиль хранится без истории, поэтому за прошлые дни берется текущий
    /// профиль и вес того дня.
    /// </summary>
    public const string TargetNote =
        "Цель на каждую дату пересчитана по весу того дня; профиль взят текущий - историю профиля пока не храним.";
}
