using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Norma.Api.Data;
using Norma.Api.Data.Entities;
using Norma.Api.Features.Auth;
using Norma.Core.Age;

namespace Norma.Api.Features.Profiles.UpdateProfile;

/// <summary>
/// Срез «обновить профиль»: полная замена, поэтому не переданные поля стираются.
/// Клиент присылает форму целиком.
/// </summary>
public static class UpdateProfileEndpoint
{
    private const double MinHeightCm = 100;
    private const double MaxHeightCm = 250;
    private const double MinTargetKg = 20;
    private const double MaxTargetKg = 500;
    private const double MaxKgPerWeek = 1.5;
    private const int MinAge = 14;
    private const int MaxAge = 120;

    public static IEndpointRouteBuilder MapUpdateProfile(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPut("/me/profile", Handle)
            .WithName("UpdateProfile")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<Results<Ok<ProfileResponse>, ValidationProblem, UnauthorizedHttpResult>> Handle(
        UpdateProfileRequest? request,
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

        if (request is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["body"] = ["Нужно тело запроса с профилем."],
            });
        }

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

        if (request.BirthDate is DateOnly birthDate)
        {
            var age = AgeMath.Years(birthDate, today);

            if (birthDate > today || age is < MinAge or > MaxAge)
            {
                errors["birthDate"] = [$"Возраст должен быть от {MinAge} до {MaxAge} лет, дата не может быть в будущем."];
            }
        }

        if (request.HeightCm is double height && (height < MinHeightCm || height > MaxHeightCm))
        {
            errors["heightCm"] = [$"Рост должен быть в диапазоне {MinHeightCm}-{MaxHeightCm} см."];
        }

        if (request.TargetWeightKg is double targetWeightKg
            && (targetWeightKg < MinTargetKg || targetWeightKg > MaxTargetKg))
        {
            errors["targetWeightKg"] = [$"Желаемый вес должен быть в диапазоне {MinTargetKg}-{MaxTargetKg} кг."];
        }

        if (request.KgPerWeek is double kgPerWeek && (kgPerWeek < 0 || kgPerWeek > MaxKgPerWeek))
        {
            errors["kgPerWeek"] = [$"Темп должен быть в диапазоне 0-{MaxKgPerWeek} кг/нед."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var profile = await db.UserProfiles
            .FirstOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken);

        if (profile is null)
        {
            profile = new UserProfile { UserId = userId.Value };
            db.UserProfiles.Add(profile);
        }

        profile.Sex = request.Sex;
        profile.BirthDate = request.BirthDate;
        profile.HeightCm = request.HeightCm;
        profile.Activity = request.Activity;
        profile.Goal = request.Goal;
        profile.TargetWeightKg = request.TargetWeightKg;
        profile.KgPerWeek = request.KgPerWeek;
        profile.UpdatedAt = clock.GetUtcNow();

        // Снимок действует с сегодняшнего дня: статистика за прошлые дни возьмет
        // прошлый снимок, а не этот, и цель истории не поедет задним числом.
        db.UserProfileSnapshots.Add(new UserProfileSnapshot
        {
            Id = Guid.NewGuid(),
            UserId = userId.Value,
            EffectiveFrom = today,
            CreatedAt = profile.UpdatedAt,
            Sex = profile.Sex,
            BirthDate = profile.BirthDate,
            HeightCm = profile.HeightCm,
            Activity = profile.Activity,
            Goal = profile.Goal,
            TargetWeightKg = profile.TargetWeightKg,
            KgPerWeek = profile.KgPerWeek,
        });

        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new ProfileResponse(
            profile.Sex,
            profile.BirthDate,
            profile.HeightCm,
            profile.Activity,
            profile.Goal,
            profile.TargetWeightKg,
            profile.KgPerWeek,
            profile.UpdatedAt));
    }
}
