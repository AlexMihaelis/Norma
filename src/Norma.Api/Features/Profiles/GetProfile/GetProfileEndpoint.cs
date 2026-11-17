using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Norma.Api.Data;
using Norma.Api.Features.Auth;

namespace Norma.Api.Features.Profiles.GetProfile;

/// <summary>Срез «мой профиль»: только свои данные, из токена.</summary>
public static class GetProfileEndpoint
{
    public static IEndpointRouteBuilder MapGetProfile(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet("/me/profile", Handle)
            .WithName("GetProfile")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<Results<Ok<ProfileResponse>, NotFound, UnauthorizedHttpResult>> Handle(
        ClaimsPrincipal principal,
        NormaDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();

        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        var profile = await db.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken);

        if (profile is null)
        {
            return TypedResults.NotFound();
        }

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
