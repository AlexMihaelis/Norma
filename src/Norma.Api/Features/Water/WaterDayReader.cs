using Microsoft.EntityFrameworkCore;
using Norma.Api.Data;
using Norma.Core.Energy;
using Norma.Core.Water;

namespace Norma.Api.Features.Water;

/// <summary>
/// Чтение воды за день: сумма приемов и ориентир по профилю. Общая операция для
/// просмотра дня и для отмены последнего приема.
/// </summary>
internal static class WaterDayReader
{
    public static async Task<WaterDayResponse> ReadAsync(
        NormaDbContext db,
        Guid userId,
        DateOnly day,
        CancellationToken cancellationToken)
    {
        var drunkMl = await db.WaterIntakes
            .Where(intake => intake.UserId == userId && intake.Date == day)
            .Select(intake => (int?)intake.Ml)
            .SumAsync(cancellationToken) ?? 0;

        var profile = await db.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken);

        // Последний замер: свежая дата важнее порядка ввода.
        var weightKg = await db.WeightMeasurements
            .Where(measurement => measurement.UserId == userId)
            .OrderByDescending(measurement => measurement.Date)
            .ThenByDescending(measurement => measurement.CreatedAt)
            .Select(measurement => (double?)measurement.Kg)
            .FirstOrDefaultAsync(cancellationToken);

        var sex = profile?.Sex;

        if (sex is null || weightKg is null)
        {
            return new WaterDayResponse(
                day,
                drunkMl,
                NormMl: null,
                BaseMl: null,
                ActivityBonusMl: null,
                RemainingMl: null,
                ClampedToMin: false,
                ClampedToMax: false,
                MissingDataHint(sex, weightKg),
                WaterText.Disclaimer);
        }

        // Без активности поправка 0, но норму не скрываем.
        var activity = profile!.Activity ?? ActivityLevel.Sedentary;

        var target = WaterMath.ForProfile(sex.Value, weightKg.Value, activity);

        var normHint = profile.Activity is null
            ? "Уровень активности в профиле не указан - поправка на активность не применена."
            : null;

        return new WaterDayResponse(
            day,
            drunkMl,
            target.NormMl,
            target.BaseMl,
            target.ActivityBonusMl,
            target.NormMl - drunkMl,
            target.ClampedToMin,
            target.ClampedToMax,
            normHint,
            WaterText.Disclaimer);
    }

    private static string MissingDataHint(Sex? sex, double? weightKg) => (sex, weightKg) switch
    {
        (null, null) => "Заполните профиль и запишите вес - без пола и веса норму не посчитать.",
        (null, _) => "Укажите пол в профиле - от него зависит норма на килограмм веса.",
        (_, null) => "Запишите вес - норма считается от последнего замера.",
        _ => "Норму посчитать не удалось.",
    };
}
