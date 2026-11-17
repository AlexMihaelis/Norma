namespace Norma.Api.Features.Water;

/// <summary>Тексты, которые API отдает клиенту как есть.</summary>
public static class WaterText
{
    /// <summary>
    /// Обязательная оговорка: норма - ориентир, а не медицинское предписание.
    /// Клиент обязан показывать её рядом с числом.
    /// </summary>
    public const string Disclaimer =
        "Ориентир, а не медицинская рекомендация: при беременности, лактации, заболеваниях и нагрузках на выносливость норма может не подходить.";
}

/// <summary>
/// Вода за день: сколько выпито, ориентир и остаток. Числа нормы nullable:
/// без пола в профиле или без веса её не посчитать, тогда <c>NormHint</c>
/// объясняет, чего не хватает. <c>RemainingMl</c> - норма минус выпитое;
/// отрицательное значение означает, что норма перевыполнена.
/// </summary>
public sealed record WaterDayResponse(
    DateOnly Date,
    int DrunkMl,
    int? NormMl,
    int? BaseMl,
    int? ActivityBonusMl,
    int? RemainingMl,
    bool ClampedToMin,
    bool ClampedToMax,
    string? NormHint,
    string Disclaimer);

/// <summary>Тело записи о воде: миллилитры и дата (по умолчанию сегодня).</summary>
public sealed record AddWaterRequest(int? Ml, DateOnly? Date);

/// <summary>Сохраненный прием воды.</summary>
public sealed record WaterIntakeResponse(Guid Id, DateOnly Date, int Ml);
