using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Norma.Api.Data;
using Norma.Api.Features.Auth;
using Norma.Core.Energy;
using Norma.Core.Water;

namespace Norma.Api.Features.Water.GetWaterDay;

/// <summary>
/// Срез «вода за день»: сумма приемов и ориентир по профилю. Не хватает пола
/// или веса - норма не выдумывается, ответ подсказывает, что заполнить.
/// </summary>
public static class GetWaterDayEndpoint
{
    public static IEndpointRouteBuilder MapGetWaterDay(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet("/water", Handle)
            .WithName("GetWaterDay")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<Results<Ok<WaterDayResponse>, UnauthorizedHttpResult>> Handle(
        DateOnly? date,
        ClaimsPrincipal principal,
        NormaDbContext db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();

        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        var day = date ?? DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

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
            return TypedResults.Ok(new WaterDayResponse(
                day,
                drunkMl,
                NormMl: null,
                BaseMl: null,
                ActivityBonusMl: null,
                RemainingMl: null,
                ClampedToMin: false,
                ClampedToMax: false,
                MissingDataHint(sex, weightKg),
                WaterText.Disclaimer));
        }

        // Без активности поправка 0, но норму не скрываем.
        var activity = profile!.Activity ?? ActivityLevel.Sedentary;

        var target = WaterMath.ForProfile(sex.Value, weightKg.Value, activity);

        var normHint = profile.Activity is null
            ? "Уровень активности в профиле не указан - поправка на активность не применена."
            : null;

        return TypedResults.Ok(new WaterDayResponse(
            day,
            drunkMl,
            target.NormMl,
            target.BaseMl,
            target.ActivityBonusMl,
            target.NormMl - drunkMl,
            target.ClampedToMin,
            target.ClampedToMax,
            normHint,
            WaterText.Disclaimer));
    }

    private static string MissingDataHint(Sex? sex, double? weightKg) => (sex, weightKg) switch
    {
        (null, null) => "Заполните профиль и запишите вес - без пола и веса норму не посчитать.",
        (null, _) => "Укажите пол в профиле - от него зависит норма на килограмм веса.",
        (_, null) => "Запишите вес - норма считается от последнего замера.",
        _ => "Норму посчитать не удалось.",
    };
}
